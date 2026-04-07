using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using _3D_Export_Views.Handlers;
using _3D_Export_Views.Logic;

using DataGridCell = System.Windows.Controls.DataGridCell;

namespace _3D_Export_Views.Views
{
    public partial class _3DExportViews_Form : Window
    {
        private readonly ExternalEvent _createEvent;
        private readonly CreateViewsHandler _createHandler;
        private readonly ExternalEvent _activateEvent;
        private readonly ActivateViewHandler _activateHandler;
        private readonly ExternalEvent _refreshEvent;
        private readonly RefreshHandler _refreshHandler;

        private readonly List<PlanItem> _planItems;
        private ICollectionView _plansView;
        private int? _lastClickedIndex;
        private List<ResultItem> _results = new List<ResultItem>();

        public _3DExportViews_Form(
            ExternalEvent createEvent,
            CreateViewsHandler createHandler,
            ExternalEvent activateEvent,
            ActivateViewHandler activateHandler,
            ExternalEvent refreshEvent,
            RefreshHandler refreshHandler,
            List<ViewPlan> plans,
            List<View3D> templates)
        {
            InitializeComponent();

            Debug.WriteLine($"[_3DExportViews_Form] Constructor — {plans.Count} plans, {templates.Count} templates");

            _createEvent = createEvent;
            _createHandler = createHandler;
            _activateEvent = activateEvent;
            _activateHandler = activateHandler;
            _refreshEvent = refreshEvent;
            _refreshHandler = refreshHandler;
            _planItems = plans.Select(viewPlan => new PlanItem { Plan = viewPlan }).ToList();

            // Set up views DataGrid with filtering
            dgViews.ItemsSource = _planItems;
            _plansView = CollectionViewSource.GetDefaultView(_planItems);
            _plansView.Filter = FilterPlans;
            dgViews.PreviewMouseLeftButtonDown += DgViews_PreviewMouseLeftButtonDown;

            // Keep the selection count label in sync whenever any checkbox changes
            SubscribePlanItemPropertyChanged(_planItems);
            UpdateSelectionCount();

            // Set up discipline dropdown
            cbDiscipline.Items.Add("MECH");
            cbDiscipline.Items.Add("ELEC");
            cbDiscipline.Items.Add("PLUM");
            cbDiscipline.Items.Add("ARCH");
            cbDiscipline.SelectedIndex = 0;

            // Set up template dropdown
            PopulateTemplateDropdown(templates);

            // Wire up callbacks
            _createHandler.OnCompleted = OnViewsCreated;
            _createHandler.OnProgress = OnViewProgress;
            _refreshHandler.OnCompleted = OnRefreshCompleted;
        }

        /// <summary>
        /// Subscribes to PropertyChanged on each PlanItem so the selection count label stays in sync.
        /// </summary>
        private void SubscribePlanItemPropertyChanged(List<PlanItem> planItems)
        {
            foreach (var planItem in planItems)
                planItem.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(PlanItem.IsSelected))
                        UpdateSelectionCount();
                };
        }

        /// <summary>
        /// Populates the template ComboBox with a "None" entry followed by all 3D view templates.
        /// </summary>
        private void PopulateTemplateDropdown(List<View3D> templates)
        {
            var templateItems = new List<TemplateItem>();
            templateItems.Add(new TemplateItem { Name = "<None>", Id = ElementId.InvalidElementId });
            foreach (var template in templates)
            {
                templateItems.Add(new TemplateItem { Name = template.Name, Id = template.Id });
            }
            cbTemplate.ItemsSource = templateItems;
            cbTemplate.SelectedIndex = 0;
        }

        private bool FilterPlans(object item)
        {
            if (string.IsNullOrWhiteSpace(tbSearch.Text))
                return true;

            var planItem = item as PlanItem;
            return planItem != null && planItem.Name.IndexOf(tbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs args)
        {
            _plansView?.Refresh();
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            tbSelectionCount.Text = $"{_planItems.Count(planItem => planItem.IsSelected)} of {_planItems.Count} selected";
        }

        // ── Refresh ─────────────────────────────────────────────────────────────

        private void BtnRefresh_Click(object sender, RoutedEventArgs args)
        {
            Debug.WriteLine("[_3DExportViews_Form] Refresh clicked");
            btnRefresh.IsEnabled = false;
            _refreshEvent.Raise();
        }

        private void OnRefreshCompleted(List<ViewPlan> plans, List<View3D> templates)
        {
            Dispatcher.Invoke(() =>
            {
                Debug.WriteLine($"[_3DExportViews_Form] OnRefreshCompleted — {plans.Count} plans, {templates.Count} templates");

                // Clear search box so the filter doesn't hide refreshed items
                tbSearch.Text = "";

                // Rebuild plan items
                _planItems.Clear();
                _planItems.AddRange(plans.Select(viewPlan => new PlanItem { Plan = viewPlan }));
                SubscribePlanItemPropertyChanged(_planItems);

                dgViews.ItemsSource = null;
                dgViews.ItemsSource = _planItems;
                _plansView = CollectionViewSource.GetDefaultView(_planItems);
                _plansView.Filter = FilterPlans;
                _lastClickedIndex = null;

                // Rebuild template dropdown
                PopulateTemplateDropdown(templates);

                // Clear results
                _results.Clear();
                lbResults.ItemsSource = null;
                tbResultCounts.Text = "";

                UpdateSelectionCount();
                btnRefresh.IsEnabled = true;
            });
        }

        // ── Toggle-all / checkbox click ─────────────────────────────────────────

        /// <summary>
        /// Toggles only the currently visible (filtered) rows.
        /// </summary>
        private void HeaderToggleAll_Click(object sender, RoutedEventArgs args)
        {
            var visibleItems = _plansView.Cast<PlanItem>().ToList();
            bool anyUnchecked = visibleItems.Any(planItem => !planItem.IsSelected);

            Debug.WriteLine($"[_3DExportViews_Form] HeaderToggleAll — {visibleItems.Count} visible, "
                + $"anyUnchecked={anyUnchecked}, setting all to {anyUnchecked}");

            foreach (var planItem in visibleItems)
                planItem.IsSelected = anyUnchecked;
        }

        /// <summary>
        /// Handles checkbox click behavior: plain click toggles one row,
        /// Shift+click selects a range within visible rows, multi-highlighted
        /// click toggles all highlighted rows together.
        /// </summary>
        private void DgViews_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            var visualElement = (DependencyObject)args.OriginalSource;
            while (visualElement != null && visualElement is not DataGridCell)
                visualElement = VisualTreeHelper.GetParent(visualElement);

            if (visualElement is DataGridCell cell &&
                cell.Column is DataGridCheckBoxColumn &&
                cell.DataContext is PlanItem clickedItem)
            {
                // Always work against the currently visible (filtered) items
                var visibleItems = _plansView.Cast<PlanItem>().ToList();
                int clickedIndex = visibleItems.IndexOf(clickedItem);
                bool newState;

                var highlightedItems = dgViews.SelectedItems.OfType<PlanItem>().ToList();

                if (highlightedItems.Count <= 1)
                {
                    if (Keyboard.IsKeyDown(Key.LeftShift)
                        && _lastClickedIndex.HasValue
                        && _lastClickedIndex.Value < visibleItems.Count)
                    {
                        // Range-select within visible rows only
                        int rangeStart = Math.Min(_lastClickedIndex.Value, clickedIndex);
                        int rangeEnd   = Math.Max(_lastClickedIndex.Value, clickedIndex);
                        newState       = !clickedItem.IsSelected;

                        Debug.WriteLine($"[_3DExportViews_Form] Shift+click range [{rangeStart}..{rangeEnd}] -> {newState}");

                        for (int rangeIndex = rangeStart; rangeIndex <= rangeEnd; rangeIndex++)
                            visibleItems[rangeIndex].IsSelected = newState;
                    }
                    else
                    {
                        clickedItem.IsSelected = !clickedItem.IsSelected;
                    }
                }
                else
                {
                    // Toggle all highlighted rows to the opposite of the clicked row's state
                    newState = !clickedItem.IsSelected;

                    Debug.WriteLine($"[_3DExportViews_Form] Multi-select toggle — {highlightedItems.Count} items -> {newState}");

                    foreach (var highlightedItem in highlightedItems)
                        highlightedItem.IsSelected = newState;
                }

                _lastClickedIndex = clickedIndex;
                args.Handled = true;
            }
        }

        // ── Create views ────────────────────────────────────────────────────────

        private void BtnCreate_Click(object sender, RoutedEventArgs args)
        {
            // Validate selections
            var selectedPlans = _planItems.Where(planItem => planItem.IsSelected).Select(planItem => planItem.Plan).ToList();
            if (selectedPlans.Count == 0)
            {
                TaskDialog.Show("3D Export Views", "Please select at least one floor or ceiling plan.");
                return;
            }

            string discipline = cbDiscipline.Text?.Trim();
            if (string.IsNullOrEmpty(discipline))
            {
                TaskDialog.Show("3D Export Views", "Please enter or select a discipline.");
                return;
            }

            var selectedTemplate = cbTemplate.SelectedItem as TemplateItem;
            if (selectedTemplate == null)
            {
                TaskDialog.Show("3D Export Views", "Please select a view template.");
                return;
            }

            Debug.WriteLine($"[_3DExportViews_Form] Create clicked — {selectedPlans.Count} plans, "
                + $"Discipline={discipline}, Template={selectedTemplate.Name}");

            // Set handler shared state
            _createHandler.SelectedPlans = selectedPlans;
            _createHandler.TemplateId = selectedTemplate.Id;
            _createHandler.Discipline = discipline;

            // Disable create button and show progress
            btnCreate.IsEnabled = false;
            pbProgress.Value = 0;
            pbProgress.Maximum = selectedPlans.Count;
            pbProgress.Visibility = System.Windows.Visibility.Visible;
            tbProgress.Text = $"0 / {selectedPlans.Count}";
            tbProgress.Visibility = System.Windows.Visibility.Visible;

            // Force WPF to render before Revit processes the event
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Raise the external event
            _createEvent.Raise();
        }

        private void OnViewProgress(int currentCount, int totalCount)
        {
            // Update progress bar and text, then pump the WPF dispatcher to render
            pbProgress.Value = currentCount;
            tbProgress.Text = $"{currentCount} / {totalCount}";

            // Push a nested message loop so WPF processes the render queue
            var renderFrame = new System.Windows.Threading.DispatcherFrame();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(dispatcherFrame =>
                {
                    ((System.Windows.Threading.DispatcherFrame)dispatcherFrame).Continue = false;
                    return null;
                }), renderFrame);
            System.Windows.Threading.Dispatcher.PushFrame(renderFrame);
        }

        private void OnViewsCreated(List<ViewResult> results)
        {
            // Dispatch to UI thread
            Dispatcher.Invoke(() =>
            {
                Debug.WriteLine($"[_3DExportViews_Form] OnViewsCreated — {results.Count} results");

                _results.Clear();
                foreach (var viewResult in results)
                {
                    _results.Add(new ResultItem
                    {
                        ViewId = viewResult.ViewId,
                        ViewName = viewResult.ViewName,
                        Success = viewResult.Success,
                        ErrorMessage = viewResult.ErrorMessage
                    });
                }

                lbResults.ItemsSource = null;
                lbResults.ItemsSource = _results;

                // Update result counts
                int createdCount = _results.Count(result => result.Success);
                int failedCount = _results.Count(result => !result.Success);
                tbResultCounts.Text = $"({createdCount} created, {failedCount} failed)";

                Debug.WriteLine($"[_3DExportViews_Form] Results: {createdCount} created, {failedCount} failed");

                btnCreate.IsEnabled = true;
                pbProgress.Visibility = System.Windows.Visibility.Collapsed;
                tbProgress.Visibility = System.Windows.Visibility.Collapsed;
            });
        }

        // ── Result double-click / window close ──────────────────────────────────

        private void LbResults_MouseDoubleClick(object sender, MouseButtonEventArgs args)
        {
            var selectedResult = lbResults.SelectedItem as ResultItem;
            if (selectedResult == null || !selectedResult.Success || selectedResult.ViewId == ElementId.InvalidElementId)
                return;

            Debug.WriteLine($"[_3DExportViews_Form] Activating view: {selectedResult.ViewName} (Id={selectedResult.ViewId})");

            _activateHandler.ViewToActivate = selectedResult.ViewId;
            _activateEvent.Raise();
        }

        protected override void OnClosing(CancelEventArgs args)
        {
            // Hide instead of close for singleton reuse
            args.Cancel = true;
            this.Hide();
            Debug.WriteLine("[_3DExportViews_Form] Window hidden (singleton reuse)");
        }
    }

    // ── Data model classes ───────────────────────────────────────────────────

    internal class PlanItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ViewPlan Plan { get; set; }
        public string Name => Plan?.Name ?? string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal class TemplateItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    internal class ResultItem
    {
        public ElementId ViewId { get; set; }
        public string ViewName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public string DisplayText
        {
            get
            {
                if (Success)
                    // U+2713 = ✓ Check Mark
                    return $"\u2713 {ViewName} (double-click to open)";
                else
                    // U+2717 = ✗ Ballot X
                    return $"\u2717 {ViewName} - {ErrorMessage}";
            }
        }
    }
}

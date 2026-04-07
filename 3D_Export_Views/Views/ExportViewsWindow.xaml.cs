using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using _3D_Export_Views.Handlers;
using _3D_Export_Views.Logic;

namespace _3D_Export_Views.Views
{
    public partial class ExportViewsWindow : Window
    {
        private readonly ExternalEvent _createEvent;
        private readonly CreateViewsHandler _createHandler;
        private readonly ExternalEvent _activateEvent;
        private readonly ActivateViewHandler _activateHandler;

        private readonly List<ViewPlan> _allPlans;
        private ICollectionView _plansView;
        private List<ResultItem> _results = new List<ResultItem>();

        public ExportViewsWindow(
            ExternalEvent createEvent,
            CreateViewsHandler createHandler,
            ExternalEvent activateEvent,
            ActivateViewHandler activateHandler,
            List<ViewPlan> plans,
            List<View3D> templates)
        {
            InitializeComponent();

            _createEvent = createEvent;
            _createHandler = createHandler;
            _activateEvent = activateEvent;
            _activateHandler = activateHandler;
            _allPlans = plans;

            // Set up views list with filtering
            lbViews.ItemsSource = _allPlans;
            _plansView = CollectionViewSource.GetDefaultView(_allPlans);
            _plansView.Filter = FilterPlans;

            // Set up discipline dropdown
            cbDiscipline.Items.Add("MECH");
            cbDiscipline.Items.Add("ELEC");
            cbDiscipline.Items.Add("PLUM");
            cbDiscipline.Items.Add("ARCH");
            cbDiscipline.SelectedIndex = 0;

            // Set up template dropdown
            var templateItems = new List<TemplateItem>();
            templateItems.Add(new TemplateItem { Name = "<None>", Id = ElementId.InvalidElementId });
            foreach (var t in templates)
            {
                templateItems.Add(new TemplateItem { Name = t.Name, Id = t.Id });
            }
            cbTemplate.ItemsSource = templateItems;
            cbTemplate.SelectedIndex = 0;

            // Wire up results callback
            _createHandler.OnCompleted = OnViewsCreated;
        }

        private bool FilterPlans(object item)
        {
            if (string.IsNullOrWhiteSpace(tbSearch.Text))
                return true;

            var plan = item as ViewPlan;
            return plan != null && plan.Name.IndexOf(tbSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _plansView?.Refresh();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lbViews.SelectAll();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            lbViews.UnselectAll();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            // Validate selections
            if (lbViews.SelectedItems.Count == 0)
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

            // Set handler shared state
            _createHandler.SelectedPlans = lbViews.SelectedItems.Cast<ViewPlan>().ToList();
            _createHandler.TemplateId = selectedTemplate.Id;
            _createHandler.Discipline = discipline;

            // Disable create button while processing
            btnCreate.IsEnabled = false;

            // Raise the external event
            _createEvent.Raise();
        }

        private void OnViewsCreated(List<ViewResult> results)
        {
            // Dispatch to UI thread
            Dispatcher.Invoke(() =>
            {
                _results.Clear();
                foreach (var r in results)
                {
                    _results.Add(new ResultItem
                    {
                        ViewId = r.ViewId,
                        ViewName = r.ViewName,
                        Success = r.Success,
                        ErrorMessage = r.ErrorMessage
                    });
                }

                lbResults.ItemsSource = null;
                lbResults.ItemsSource = _results;

                btnCreate.IsEnabled = true;
            });
        }

        private void LbResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = lbResults.SelectedItem as ResultItem;
            if (selected == null || !selected.Success || selected.ViewId == ElementId.InvalidElementId)
                return;

            _activateHandler.ViewToActivate = selected.ViewId;
            _activateEvent.Raise();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Hide instead of close for singleton reuse
            e.Cancel = true;
            this.Hide();
        }
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
                    return $"\u2713 {ViewName} (double-click to open)";
                else
                    return $"\u2717 {ViewName} - {ErrorMessage}";
            }
        }
    }
}

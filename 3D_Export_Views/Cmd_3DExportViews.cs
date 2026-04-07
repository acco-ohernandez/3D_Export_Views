using System.Collections.Generic;
using System.Linq;
using _3D_Export_Views.Handlers;
using _3D_Export_Views.Views;

namespace _3D_Export_Views
{
    [Transaction(TransactionMode.Manual)]
    public class Cmd_3DExportViews : IExternalCommand
    {
        private static _3DExportViews_Form _window;
        private static ExternalEvent _createEvent;
        private static CreateViewsHandler _createHandler;
        private static ExternalEvent _activateEvent;
        private static ActivateViewHandler _activateHandler;
        private static ExternalEvent _refreshEvent;
        private static RefreshHandler _refreshHandler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Debug.WriteLine("[Cmd_3DExportViews] Execute started");

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // If window already exists and is visible, just focus it
            if (_window != null && _window.IsVisible)
            {
                Debug.WriteLine("[Cmd_3DExportViews] Window already visible — activating");
                _window.Activate();
                return Result.Succeeded;
            }

            // Collect floor plans and ceiling plans (exclude templates)
            List<ViewPlan> plans = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(viewPlan => !viewPlan.IsTemplate
                    && (viewPlan.ViewType == ViewType.FloorPlan || viewPlan.ViewType == ViewType.CeilingPlan))
                .OrderBy(viewPlan => viewPlan.Name)
                .ToList();

            Debug.WriteLine($"[Cmd_3DExportViews] Found {plans.Count} plans");

            if (plans.Count == 0)
            {
                Debug.WriteLine("[Cmd_3DExportViews] No plans found — cancelled");
                TaskDialog.Show("3D Export Views", "No floor plans or ceiling plans found in the current document.");
                return Result.Cancelled;
            }

            // Collect 3D view templates
            List<View3D> templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(view3d => view3d.IsTemplate)
                .OrderBy(view3d => view3d.Name)
                .ToList();

            // Find the 3D ViewFamilyType
            ElementId viewFamilyTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .First(familyType => familyType.ViewFamily == ViewFamily.ThreeDimensional)
                .Id;

            // Create handlers and ExternalEvents (once)
            if (_createHandler == null)
            {
                _createHandler = new CreateViewsHandler();
                _createEvent = ExternalEvent.Create(_createHandler);
            }

            if (_activateHandler == null)
            {
                _activateHandler = new ActivateViewHandler();
                _activateEvent = ExternalEvent.Create(_activateHandler);
            }

            if (_refreshHandler == null)
            {
                _refreshHandler = new RefreshHandler();
                _refreshEvent = ExternalEvent.Create(_refreshHandler);
            }

            // Store the ViewFamilyTypeId on the handler
            _createHandler.ViewFamilyTypeId = viewFamilyTypeId;

            // Create and show the modeless window
            _window = new _3DExportViews_Form(
                _createEvent,
                _createHandler,
                _activateEvent,
                _activateHandler,
                _refreshEvent,
                _refreshHandler,
                plans,
                templates);

            _window.Show();

            Debug.WriteLine($"[Cmd_3DExportViews] Window shown — {plans.Count} plans, "
                + $"{templates.Count} templates, ViewFamilyTypeId={viewFamilyTypeId}");

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btn3DExportViews";
            string buttonTitle = "3D Export\nViews";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "Create 3D isometric views from selected floor and ceiling plans with section boxes and view templates.");

            return myButtonData.Data;
        }
    }
}

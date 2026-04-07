using System;
using System.Collections.Generic;
using System.Linq;

namespace _3D_Export_Views.Handlers
{
    public class RefreshHandler : IExternalEventHandler
    {
        // Callback to send refreshed data back to the UI
        public Action<List<ViewPlan>, List<View3D>> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Debug.WriteLine("[RefreshHandler] Execute started");

                Document doc = app.ActiveUIDocument.Document;

                // Re-collect floor plans and ceiling plans (exclude templates)
                List<ViewPlan> plans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(viewPlan => !viewPlan.IsTemplate
                        && (viewPlan.ViewType == ViewType.FloorPlan || viewPlan.ViewType == ViewType.CeilingPlan))
                    .OrderBy(viewPlan => viewPlan.Name)
                    .ToList();

                // Re-collect 3D view templates
                List<View3D> templates = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(view3d => view3d.IsTemplate)
                    .OrderBy(view3d => view3d.Name)
                    .ToList();

                Debug.WriteLine($"[RefreshHandler] Found {plans.Count} plans, {templates.Count} templates");
                OnCompleted?.Invoke(plans, templates);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"[RefreshHandler] UNHANDLED EXCEPTION: {exception}");

                // Invoke with empty lists so the UI re-enables the refresh button
                OnCompleted?.Invoke(new List<ViewPlan>(), new List<View3D>());
            }
        }

        public string GetName()
        {
            return "Refresh 3D Export Views";
        }
    }
}

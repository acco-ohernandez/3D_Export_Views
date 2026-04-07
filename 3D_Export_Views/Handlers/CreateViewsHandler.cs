using System;
using System.Collections.Generic;
using _3D_Export_Views.Logic;

namespace _3D_Export_Views.Handlers
{
    public class CreateViewsHandler : IExternalEventHandler
    {
        // Shared state — set by the window before raising the event
        public List<ViewPlan> SelectedPlans { get; set; }
        public ElementId ViewFamilyTypeId { get; set; }
        public ElementId TemplateId { get; set; }
        public string Discipline { get; set; }

        // Callback to send results back to the UI
        public Action<List<ViewResult>> OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            List<ViewResult> results = ViewCreator.CreateViews(doc, SelectedPlans, ViewFamilyTypeId, TemplateId, Discipline);
            OnCompleted?.Invoke(results);
        }

        public string GetName()
        {
            return "Create 3D Export Views";
        }
    }
}

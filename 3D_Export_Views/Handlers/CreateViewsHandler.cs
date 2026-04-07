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

        // Callbacks to send progress and results back to the UI
        public Action<List<ViewResult>> OnCompleted { get; set; }
        public Action<int, int> OnProgress { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Debug.WriteLine("[CreateViewsHandler] Execute started — "
                    + $"{SelectedPlans?.Count ?? 0} plans, Discipline={Discipline}, TemplateId={TemplateId}");

                Document doc = app.ActiveUIDocument.Document;
                List<ViewResult> results = ViewCreator.CreateViews(
                    doc, SelectedPlans, ViewFamilyTypeId, TemplateId, Discipline, OnProgress);

                Debug.WriteLine($"[CreateViewsHandler] Execute completed — {results.Count} results");
                OnCompleted?.Invoke(results);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"[CreateViewsHandler] UNHANDLED EXCEPTION: {exception}");

                // Return a single failure result so the UI is notified and re-enabled
                var fallbackResults = new List<ViewResult>
                {
                    new ViewResult
                    {
                        ViewId = ElementId.InvalidElementId,
                        ViewName = "Batch operation",
                        Success = false,
                        ErrorMessage = exception.Message
                    }
                };
                OnCompleted?.Invoke(fallbackResults);
            }
        }

        public string GetName()
        {
            return "Create 3D Export Views";
        }
    }
}

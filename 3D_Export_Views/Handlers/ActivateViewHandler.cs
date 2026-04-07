using System;

namespace _3D_Export_Views.Handlers
{
    public class ActivateViewHandler : IExternalEventHandler
    {
        // Shared state — set by the window before raising the event
        public ElementId ViewToActivate { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (ViewToActivate == null || ViewToActivate == ElementId.InvalidElementId)
                {
                    Debug.WriteLine("[ActivateViewHandler] Skipped — ViewToActivate is null or invalid");
                    return;
                }

                Debug.WriteLine($"[ActivateViewHandler] Activating view with ElementId={ViewToActivate}");

                Document doc = app.ActiveUIDocument.Document;
                View view = doc.GetElement(ViewToActivate) as View;

                if (view != null)
                {
                    app.ActiveUIDocument.ActiveView = view;
                    Debug.WriteLine($"[ActivateViewHandler] Activated view: {view.Name}");
                }
                else
                {
                    Debug.WriteLine($"[ActivateViewHandler] WARNING: ElementId={ViewToActivate} did not resolve to a View");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"[ActivateViewHandler] UNHANDLED EXCEPTION: {exception}");
            }
        }

        public string GetName()
        {
            return "Activate 3D View";
        }
    }
}

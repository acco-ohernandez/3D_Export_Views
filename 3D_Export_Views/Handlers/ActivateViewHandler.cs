namespace _3D_Export_Views.Handlers
{
    public class ActivateViewHandler : IExternalEventHandler
    {
        // Shared state — set by the window before raising the event
        public ElementId ViewToActivate { get; set; }

        public void Execute(UIApplication app)
        {
            if (ViewToActivate == null || ViewToActivate == ElementId.InvalidElementId)
                return;

            Document doc = app.ActiveUIDocument.Document;
            View view = doc.GetElement(ViewToActivate) as View;

            if (view != null)
            {
                app.ActiveUIDocument.ActiveView = view;
            }
        }

        public string GetName()
        {
            return "Activate 3D View";
        }
    }
}

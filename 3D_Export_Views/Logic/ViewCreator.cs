using System.Collections.Generic;
using System.Linq;

namespace _3D_Export_Views.Logic
{
    public class ViewResult
    {
        public ElementId ViewId { get; set; }
        public string ViewName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class ViewCreator
    {
        public static List<ViewResult> CreateViews(
            Document doc,
            List<ViewPlan> sourcePlans,
            ElementId viewFamilyTypeId,
            ElementId templateId,
            string discipline)
        {
            var results = new List<ViewResult>();

            using (Transaction t = new Transaction(doc, "Create 3D Export Views"))
            {
                t.Start();

                ViewOrientation3D orientation = GetDefaultOrientation(doc);

                foreach (ViewPlan plan in sourcePlans)
                {
                    try
                    {
                        View3D view3d = CreateSingle3DView(doc, plan, viewFamilyTypeId, templateId, discipline, orientation);
                        results.Add(new ViewResult
                        {
                            ViewId = view3d.Id,
                            ViewName = view3d.Name,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ViewResult
                        {
                            ViewId = ElementId.InvalidElementId,
                            ViewName = $"3D - {discipline} - {plan.GenLevel?.Name ?? plan.Name}",
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                t.Commit();
            }

            return results;
        }

        private static View3D CreateSingle3DView(
            Document doc,
            ViewPlan sourcePlan,
            ElementId viewFamilyTypeId,
            ElementId templateId,
            string discipline,
            ViewOrientation3D orientation)
        {
            // 1. Create the 3D view and orient it
            View3D view3d = View3D.CreateIsometric(doc, viewFamilyTypeId);
            view3d.SetOrientation(orientation);

            // 2. Apply view template FIRST (before section box) to prevent template override
            if (templateId != ElementId.InvalidElementId)
            {
                view3d.ViewTemplateId = templateId;
            }

            // 3. Compute and apply section box
            BoundingBoxXYZ sectionBox = ComputeSectionBox(doc, sourcePlan);
            view3d.SetSectionBox(sectionBox);

            // 4. Name the view
            string levelName = sourcePlan.GenLevel?.Name ?? sourcePlan.Name;
            string baseName = $"3D - {discipline} - {levelName}";
            view3d.Name = GetUniqueViewName(doc, baseName);

            return view3d;
        }

        private static BoundingBoxXYZ ComputeSectionBox(Document doc, ViewPlan sourcePlan)
        {
            // Get XY extents from crop box, applying transform for rotated crops
            BoundingBoxXYZ cropBox = sourcePlan.CropBox;
            Transform transform = cropBox.Transform;

            XYZ min = cropBox.Min;
            XYZ max = cropBox.Max;

            // Transform crop box corners to model coordinates
            XYZ corner1 = transform.OfPoint(new XYZ(min.X, min.Y, 0));
            XYZ corner2 = transform.OfPoint(new XYZ(max.X, max.Y, 0));
            XYZ corner3 = transform.OfPoint(new XYZ(min.X, max.Y, 0));
            XYZ corner4 = transform.OfPoint(new XYZ(max.X, min.Y, 0));

            double minX = Math.Min(Math.Min(corner1.X, corner2.X), Math.Min(corner3.X, corner4.X));
            double minY = Math.Min(Math.Min(corner1.Y, corner2.Y), Math.Min(corner3.Y, corner4.Y));
            double maxX = Math.Max(Math.Max(corner1.X, corner2.X), Math.Max(corner3.X, corner4.X));
            double maxY = Math.Max(Math.Max(corner1.Y, corner2.Y), Math.Max(corner3.Y, corner4.Y));

            // Get Z extents from view range
            PlanViewRange viewRange = sourcePlan.GetViewRange();

            ElementId topLevelId = viewRange.GetLevelId(PlanViewPlane.TopClipPlane);
            double topOffset = viewRange.GetOffset(PlanViewPlane.TopClipPlane);
            double topZ = GetElevation(doc, topLevelId, sourcePlan) + topOffset;

            ElementId bottomLevelId = viewRange.GetLevelId(PlanViewPlane.BottomClipPlane);
            double bottomOffset = viewRange.GetOffset(PlanViewPlane.BottomClipPlane);
            double bottomZ = GetElevation(doc, bottomLevelId, sourcePlan) + bottomOffset;

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Min = new XYZ(minX, minY, bottomZ);
            sectionBox.Max = new XYZ(maxX, maxY, topZ);

            return sectionBox;
        }

        private static double GetElevation(Document doc, ElementId levelId, ViewPlan sourcePlan)
        {
            double sourceLevelElev = sourcePlan.GenLevel.Elevation;

            // PlanViewRange.Current — use the plan's own associated level
            if (levelId == PlanViewRange.Current)
            {
                return sourceLevelElev;
            }

            // PlanViewRange.LevelAbove — find the next level above
            if (levelId == PlanViewRange.LevelAbove)
            {
                Level above = GetAdjacentLevel(doc, sourceLevelElev, above: true);
                return above != null ? above.Elevation : sourceLevelElev + 100.0;
            }

            // PlanViewRange.LevelBelow — find the next level below
            if (levelId == PlanViewRange.LevelBelow)
            {
                Level below = GetAdjacentLevel(doc, sourceLevelElev, above: false);
                return below != null ? below.Elevation : sourceLevelElev;
            }

            // Explicit level ID
            Level level = doc.GetElement(levelId) as Level;
            if (level != null)
            {
                return level.Elevation;
            }

            // Fallback
            return sourceLevelElev;
        }

        private static Level GetAdjacentLevel(Document doc, double elevation, bool above)
        {
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (above)
            {
                return levels.FirstOrDefault(l => l.Elevation > elevation + 0.001);
            }
            else
            {
                return levels.LastOrDefault(l => l.Elevation < elevation - 0.001);
            }
        }

        private static ViewOrientation3D GetDefaultOrientation(Document doc)
        {
            // Try to copy orientation from the project's default {3D} view
            View3D default3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.Contains("{3D}"));

            if (default3D != null)
            {
                return default3D.GetOrientation();
            }

            // Fallback: construct a standard SE isometric orientation
            XYZ forward = new XYZ(-1, 1, -1).Normalize();
            XYZ up = XYZ.BasisZ;
            XYZ eye = XYZ.Zero;
            return new ViewOrientation3D(eye, up, forward);
        }

        private static string GetUniqueViewName(Document doc, string baseName)
        {
            HashSet<string> existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Select(v => v.Name));

            string name = baseName;
            int counter = 1;

            while (existingNames.Contains(name))
            {
                counter++;
                name = $"{baseName} ({counter})";
            }

            return name;
        }
    }
}

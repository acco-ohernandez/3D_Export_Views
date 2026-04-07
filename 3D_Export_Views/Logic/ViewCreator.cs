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
            string discipline,
            Action<int, int> onProgress = null)
        {
            var results = new List<ViewResult>();
            int totalPlans = sourcePlans.Count;
            int currentPlan = 0;

            Debug.WriteLine($"[ViewCreator] CreateViews started — {totalPlans} plans, "
                + $"ViewFamilyTypeId={viewFamilyTypeId}, TemplateId={templateId}, Discipline={discipline}");

            using (Transaction transaction = new Transaction(doc, "Create 3D Export Views"))
            {
                transaction.Start();

                ViewOrientation3D orientation = GetDefaultOrientation(doc);

                foreach (ViewPlan plan in sourcePlans)
                {
                    try
                    {
                        Debug.WriteLine($"[ViewCreator] Creating view for plan: {plan.Name} (Id={plan.Id})");

                        View3D view3d = CreateSingle3DView(doc, plan, viewFamilyTypeId, templateId, discipline, orientation);
                        results.Add(new ViewResult
                        {
                            ViewId = view3d.Id,
                            ViewName = view3d.Name,
                            Success = true
                        });

                        Debug.WriteLine($"[ViewCreator] Created: {view3d.Name} (Id={view3d.Id})");
                    }
                    catch (Exception exception)
                    {
                        string failedViewName = $"3D - {discipline} - {plan.GenLevel?.Name ?? plan.Name}";
                        Debug.WriteLine($"[ViewCreator] FAILED for plan '{plan.Name}': {exception.Message}");

                        results.Add(new ViewResult
                        {
                            ViewId = ElementId.InvalidElementId,
                            ViewName = failedViewName,
                            Success = false,
                            ErrorMessage = exception.Message
                        });
                    }

                    currentPlan++;
                    onProgress?.Invoke(currentPlan, totalPlans);
                }

                transaction.Commit();
            }

            Debug.WriteLine($"[ViewCreator] CreateViews completed — "
                + $"{results.Count(viewResult => viewResult.Success)} succeeded, "
                + $"{results.Count(viewResult => !viewResult.Success)} failed");

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
            Transform cropTransform = cropBox.Transform;

            XYZ cropMin = cropBox.Min;
            XYZ cropMax = cropBox.Max;

            // Transform crop box corners to model coordinates
            XYZ corner1 = cropTransform.OfPoint(new XYZ(cropMin.X, cropMin.Y, 0));
            XYZ corner2 = cropTransform.OfPoint(new XYZ(cropMax.X, cropMax.Y, 0));
            XYZ corner3 = cropTransform.OfPoint(new XYZ(cropMin.X, cropMax.Y, 0));
            XYZ corner4 = cropTransform.OfPoint(new XYZ(cropMax.X, cropMin.Y, 0));

            double minX = Math.Min(Math.Min(corner1.X, corner2.X), Math.Min(corner3.X, corner4.X));
            double minY = Math.Min(Math.Min(corner1.Y, corner2.Y), Math.Min(corner3.Y, corner4.Y));
            double maxX = Math.Max(Math.Max(corner1.X, corner2.X), Math.Max(corner3.X, corner4.X));
            double maxY = Math.Max(Math.Max(corner1.Y, corner2.Y), Math.Max(corner3.Y, corner4.Y));

            // Get Z extents from view range
            PlanViewRange viewRange = sourcePlan.GetViewRange();

            ElementId topLevelId = viewRange.GetLevelId(PlanViewPlane.TopClipPlane);
            double topOffset = viewRange.GetOffset(PlanViewPlane.TopClipPlane);
            double topElevation = GetElevation(doc, topLevelId, sourcePlan) + topOffset;

            ElementId bottomLevelId = viewRange.GetLevelId(PlanViewPlane.BottomClipPlane);
            double bottomOffset = viewRange.GetOffset(PlanViewPlane.BottomClipPlane);
            double bottomElevation = GetElevation(doc, bottomLevelId, sourcePlan) + bottomOffset;

            // Guard against inverted Z range (e.g., misconfigured view range)
            if (bottomElevation > topElevation)
            {
                Debug.WriteLine($"[ViewCreator] WARNING: Inverted Z range for plan '{sourcePlan.Name}' — "
                    + $"bottom={bottomElevation:F2}, top={topElevation:F2}. Swapping.");
                (bottomElevation, topElevation) = (topElevation, bottomElevation);
            }

            Debug.WriteLine($"[ViewCreator] SectionBox for '{sourcePlan.Name}': "
                + $"X=[{minX:F2}, {maxX:F2}], Y=[{minY:F2}, {maxY:F2}], Z=[{bottomElevation:F2}, {topElevation:F2}]");

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Min = new XYZ(minX, minY, bottomElevation);
            sectionBox.Max = new XYZ(maxX, maxY, topElevation);

            return sectionBox;
        }

        private static double GetElevation(Document doc, ElementId levelId, ViewPlan sourcePlan)
        {
            // Guard against null GenLevel (can happen with certain ceiling plans)
            double sourceLevelElevation = sourcePlan.GenLevel?.Elevation ?? 0.0;
            if (sourcePlan.GenLevel == null)
            {
                Debug.WriteLine($"[ViewCreator] WARNING: GenLevel is null for plan '{sourcePlan.Name}', using elevation=0.0");
            }

            // PlanViewRange.Current — use the plan's own associated level
            if (levelId == PlanViewRange.Current)
            {
                return sourceLevelElevation;
            }

            // PlanViewRange.LevelAbove — find the next level above
            if (levelId == PlanViewRange.LevelAbove)
            {
                Level levelAbove = GetAdjacentLevel(doc, sourceLevelElevation, above: true);
                return levelAbove != null ? levelAbove.Elevation : sourceLevelElevation + 100.0;
            }

            // PlanViewRange.LevelBelow — find the next level below
            if (levelId == PlanViewRange.LevelBelow)
            {
                Level levelBelow = GetAdjacentLevel(doc, sourceLevelElevation, above: false);
                return levelBelow != null ? levelBelow.Elevation : sourceLevelElevation;
            }

            // Explicit level ID
            Level explicitLevel = doc.GetElement(levelId) as Level;
            if (explicitLevel != null)
            {
                return explicitLevel.Elevation;
            }

            // Fallback
            Debug.WriteLine($"[ViewCreator] WARNING: Could not resolve levelId={levelId} for plan '{sourcePlan.Name}', "
                + $"falling back to source level elevation={sourceLevelElevation:F2}");
            return sourceLevelElevation;
        }

        private static Level GetAdjacentLevel(Document doc, double elevation, bool above)
        {
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();

            if (above)
            {
                return levels.FirstOrDefault(level => level.Elevation > elevation + 0.001);
            }
            else
            {
                return levels.LastOrDefault(level => level.Elevation < elevation - 0.001);
            }
        }

        private static ViewOrientation3D GetDefaultOrientation(Document doc)
        {
            // Try to copy orientation from the project's default {3D} view
            View3D default3DView = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(view => !view.IsTemplate && view.Name.Contains("{3D}"));

            if (default3DView != null)
            {
                Debug.WriteLine($"[ViewCreator] Using orientation from default 3D view: {default3DView.Name}");
                return default3DView.GetOrientation();
            }

            // Fallback: construct a standard SE isometric orientation
            Debug.WriteLine("[ViewCreator] No default {3D} view found — using fallback SE isometric orientation");
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
                    .Select(view => view.Name));

            string candidateName = baseName;
            int counter = 1;

            while (existingNames.Contains(candidateName))
            {
                counter++;
                candidateName = $"{baseName} ({counter})";
            }

            if (counter > 1)
            {
                Debug.WriteLine($"[ViewCreator] Name collision: '{baseName}' already exists, using '{candidateName}'");
            }

            return candidateName;
        }
    }
}

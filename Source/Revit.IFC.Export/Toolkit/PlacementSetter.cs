﻿//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Revit.IFC.Common.Utility;
using Revit.IFC.Export.Utility;

namespace Revit.IFC.Export.Toolkit
{
   /// <summary>
   ///    A state-based class that establishes the current IfcLocalPlacement applied to an element being exported.
   /// </summary>
   /// <remarks>
   ///    This class is intended to maintain the placement for the duration that it is needed.
   ///    To ensure that the lifetime of the object is correctly managed, you should declare an instance of this class as a part of a 'using' statement in C# or
   ///    similar construct in other languages.
   /// </remarks>
   public class PlacementSetter : IDisposable
   {
      class TupleElevationLevelIdComparer : IComparer<Tuple<double, ElementId>>
      {
         public int Compare(Tuple<double, ElementId> tup1, Tuple<double, ElementId> tup2)
         {
            if (MathUtil.IsAlmostEqual(tup1.Item1, tup2.Item1))
            {
               if (tup1.Item2 > tup2.Item2)
                  return 1;
               else
                  return -1;
            }
            else if (tup1.Item1 > tup2.Item1)
               return 1;
            else if (tup1.Item1 < tup2.Item1)
               return -1;
            else
               return 0;
         }
      }

      ExporterIFC m_ExporterIFC = null;
      ElementId m_LevelId = ElementId.InvalidElementId;
      IFCLevelInfo m_LevelInfo = null;
      IFCAnyHandle m_LocalPlacement = null;
      double m_Offset = 0;

      protected ExporterIFC ExporterIFC
      {
         get { return m_ExporterIFC; }
         set { m_ExporterIFC = value; }
      }

      /// <summary>
      ///    The handle to the IfcLocalPlacement stored with this setter.
      /// </summary>
      public IFCAnyHandle LocalPlacement
      {
         get { return m_LocalPlacement; }
         protected set { m_LocalPlacement = value; }
      }

      /// <summary>
      ///    The offset to the level.
      /// </summary>
      public double Offset
      {
         get { return m_Offset; }
         protected set { m_Offset = value; }
      }

      /// <summary>
      ///    The level id associated with the element and placement.
      /// </summary>
      public ElementId LevelId
      {
         get { return m_LevelId; }
         protected set { m_LevelId = value; }
      }

      /// <summary>
      ///    The level info related to the element's local placement.
      /// </summary>
      public IFCLevelInfo LevelInfo
      {
         get { return m_LevelInfo; }
         protected set { m_LevelInfo = value; }
      }

      /// <summary>
      ///    Creates a new placement setter instance for the given element.
      /// </summary>
      /// <param name="exporterIFC">The exporter.</param>
      /// <param name="element">The element.</param>
      /// <returns>The placement setter.</returns>
      public static PlacementSetter Create(ExporterIFC exporterIFC, Element elem)
      {
         return new PlacementSetter(exporterIFC, elem, null, null, LevelUtil.GetBaseLevelIdForElement(elem));
      }

      /// <summary>
      ///    Creates a new placement setter instance for the given element with the ability to specific overridden transformations.
      /// </summary>
      /// <param name="exporterIFC">The exporter.</param>
      /// <param name="element">The element.</param>
      /// <param name="instanceOffsetTrf">The offset transformation for the instance of a type.  Optional, can be <see langword="null"/>.</param>
      /// <param name="orientationTrf">The orientation transformation for the local coordinates being used to export the element.  
      /// Optional, can be <see langword="null"/>.</param>
      public static PlacementSetter Create(ExporterIFC exporterIFC, Element elem, Transform instanceOffsetTrf, Transform orientationTrf)
      {
         return new PlacementSetter(exporterIFC, elem, instanceOffsetTrf, orientationTrf, LevelUtil.GetBaseLevelIdForElement(elem));
      }

      /// <summary>
      ///    Creates a new placement setter instance for the given element with the ability to specific overridden transformations
      ///    and level id.
      /// </summary>
      /// <param name="exporterIFC">The exporter.</param>
      /// <param name="element">The element.</param>
      /// <param name="instanceOffsetTrf">The offset transformation for the instance of a type.  Optional, can be <see langword="null"/>.</param>
      /// <param name="orientationTrf">The orientation transformation for the local coordinates being used to export the element.  
      /// Optional, can be <see langword="null"/>.</param>
      /// <param name="overrideLevelId">The level id to reference.  This is intended for use when splitting walls and columns by level.</param>
      public static PlacementSetter Create(ExporterIFC exporterIFC, Element elem, Transform instanceOffsetTrf, Transform orientationTrf, ElementId overrideLevelId)
      {
         if (overrideLevelId == null || overrideLevelId == ElementId.InvalidElementId)
            overrideLevelId = LevelUtil.GetBaseLevelIdForElement(elem);
         return new PlacementSetter(exporterIFC, elem, instanceOffsetTrf, orientationTrf, overrideLevelId);
      }

      /// <summary>
      ///    Constructs a new placement setter instance for the given element with the ability to specific overridden transformations
      ///    and level id.
      /// </summary>
      /// <param name="exporterIFC">The exporter.</param>
      /// <param name="element">The element.</param>
      /// <param name="instanceOffsetTrf">The offset transformation for the instance of a type.  Optional, can be <see langword="null"/>.</param>
      /// <param name="orientationTrf">The orientation transformation for the local coordinates being used to export the element.
      /// Optional, can be <see langword="null"/>.</param>
      /// <param name="overrideLevelId">The level id to reference.</param>
      public PlacementSetter(ExporterIFC exporterIFC, Element elem, Transform instanceOffsetTrf, Transform orientationTrf, ElementId overrideLevelId)
      {
         commonInit(exporterIFC, elem, instanceOffsetTrf, orientationTrf, overrideLevelId);
      }

      /// <summary>
      ///    Obtains the handle to an alternate local placement for a room-related element.
      /// </summary>
      /// <param name="element">The element.</param>
      /// <param name="placementToUse">The handle to the IfcLocalPlacement to use for the given room-related element.</param>
      /// <returns>
      ///    The id of the spatial element related to the element.  InvalidElementId if the element
      ///    is not room-related, in which case the output will contain the placement handle from
      ///    LocalPlacement.
      /// </returns>
      public ElementId UpdateRoomRelativeCoordinates(Element elem, out IFCAnyHandle placement)
      {
         placement = LocalPlacement;
         FamilyInstance famInst = elem as FamilyInstance;
         if (famInst == null)
            return ElementId.InvalidElementId;

         Element roomOrSpace = null;
         if (roomOrSpace == null)
         {
            try
            {
               roomOrSpace = ExporterCacheManager.SpaceInfoCache.ContainsRooms ? famInst.get_Room(ExporterCacheManager.ExportOptionsCache.ActivePhaseElement) : null;
            }
            catch
            {
               roomOrSpace = null;
            }
         }

         if (roomOrSpace == null)
         {
            try
            {
               roomOrSpace = ExporterCacheManager.SpaceInfoCache.ContainsSpaces ? famInst.get_Space(ExporterCacheManager.ExportOptionsCache.ActivePhaseElement) : null;
            }
            catch
            {
               roomOrSpace = null;
            }
         }

         if (roomOrSpace == null || roomOrSpace.Location == null)
            return ElementId.InvalidElementId;

         ElementId roomId = roomOrSpace.Id;
         IFCAnyHandle roomHnd = ExporterCacheManager.SpaceInfoCache.FindSpaceHandle(roomId);

         if (IFCAnyHandleUtil.IsNullOrHasNoValue(roomHnd))
            return ElementId.InvalidElementId;

         IFCAnyHandle roomPlacementHnd = IFCAnyHandleUtil.GetObjectPlacement(roomHnd);
         Transform trf = ExporterIFCUtils.GetRelativeLocalPlacementOffsetTransform(placement, roomPlacementHnd);
         placement = ExporterUtil.CreateLocalPlacement(ExporterIFC.GetFile(), roomPlacementHnd, trf.Origin, trf.BasisZ, trf.BasisX);
         return roomId;
      }

      /// <summary>
      ///    Gets the level info related to an offset of the element's local placement.
      /// </summary>
      /// <param name="offset">The vertical offset to the local placement.</param>
      /// <param name="scale">The linear scale.</param>
      /// <param name="pPlacementHnd">The handle to the new local placement.</param>
      /// <param name="pScaledOffsetFromNewLevel">The scaled offset from the new level.</param>
      /// <returns>The level info.</returns>
      public IFCLevelInfo GetOffsetLevelInfoAndHandle(double offset, double scale, Document document, out IFCAnyHandle placementHnd, out double scaledOffsetFromNewLevel)
      {
         placementHnd = null;
         scaledOffsetFromNewLevel = 0;

         double newHeight = Offset + offset;

         IDictionary<ElementId, IFCLevelInfo> levelInfos = ExporterIFC.GetLevelInfos();
         foreach (KeyValuePair<ElementId, IFCLevelInfo> levelInfoPair in levelInfos)
         {
            // the cache contains levels from all the exported documents
            // if the export is performed for a linked document, filter the levels that are not from this document
            if (ExporterCacheManager.ExportOptionsCache.ExportingLink)
            {
               Element levelElem = document.GetElement(levelInfoPair.Key);
               if (levelElem == null || !(levelElem is Level))
                  continue;
            }

            IFCLevelInfo levelInfo = levelInfoPair.Value;
            double startHeight = levelInfo.Elevation;

            if (startHeight > newHeight + MathUtil.Eps())
               continue;

            double height = levelInfo.DistanceToNextLevel;
            bool useHeight = !MathUtil.IsAlmostZero(height);

            if (!useHeight)
            {
               scaledOffsetFromNewLevel = (newHeight - startHeight) * scale;
               placementHnd = levelInfo.GetLocalPlacement();
               return levelInfo;
            }

            double endHeight = startHeight + height;
            if (newHeight < endHeight - MathUtil.Eps())
            {
               scaledOffsetFromNewLevel = (newHeight - startHeight) * scale;
               placementHnd = levelInfo.GetLocalPlacement();
               return levelInfo;
            }
         }

         return null;
      }

      /// <summary>
      /// Attempt to determine the local placement of the element based on the element type and initial input.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC class.</param>
      /// <param name="elem">The element being exported.</param>
      /// <param name="familyTrf">The optional family transform.</param>
      /// <param name="orientationTrf">The optional orientation of the element based on IFC standards or agreements.</param>
      /// <param name="overrideLevelId">The optional level to place the element, to be used instead of heuristics.</param>
      private void commonInit(ExporterIFC exporterIFC, Element elem, Transform familyTrf, Transform orientationTrf, ElementId overrideLevelId)
      {
         ExporterIFC = exporterIFC;

         // Convert null value to InvalidElementId.
         if (overrideLevelId == null)
            overrideLevelId = ElementId.InvalidElementId;

         Document doc = elem.Document;
         Element hostElem = elem;
         ElementId elemId = elem.Id;
         ElementId newLevelId = overrideLevelId;

         bool useOverrideOrigin = false;
         XYZ overrideOrigin = XYZ.Zero;

         IDictionary<ElementId, IFCLevelInfo> levelInfos = exporterIFC.GetLevelInfos();
         SortedDictionary<Tuple<double, ElementId>, IFCLevelInfo> sortedLevelInfos = new SortedDictionary<Tuple<double, ElementId>, IFCLevelInfo>(new TupleElevationLevelIdComparer());
         foreach(KeyValuePair<ElementId,IFCLevelInfo> levelInfo in levelInfos)
         {
            sortedLevelInfos.Add(new Tuple<double,ElementId>(levelInfo.Value.Elevation, levelInfo.Key), levelInfo.Value);
         }

         if (overrideLevelId == ElementId.InvalidElementId)
         {
            if (familyTrf == null)
            {
               // Override for CurveElems -- base level calculation on origin of sketch Plane.
               if (elem is CurveElement)
               {
                  SketchPlane sketchPlane = (elem as CurveElement).SketchPlane;
                  if (sketchPlane != null)
                  {
                     useOverrideOrigin = true;
                     overrideOrigin = sketchPlane.GetPlane().Origin;
                  }
               }
               else
               {
                  ElementId hostElemId = ElementId.InvalidElementId;
                  // a bit of a hack.  If we have a railing, we want it to have the same level base as its host Stair (because of
                  // the way the stairs place railings and stair flights together).
                  if (elem is Railing)
                  {
                     hostElemId = (elem as Railing).HostId;
                  }
                  else if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Assemblies)
                  {
                     hostElemId = elem.AssemblyInstanceId;
                  }

                  if (hostElemId != ElementId.InvalidElementId)
                  {
                     hostElem = doc.GetElement(hostElemId);
                  }

                  newLevelId = hostElem != null ? hostElem.LevelId : ElementId.InvalidElementId;

                  if (newLevelId == ElementId.InvalidElementId)
                  {
                     newLevelId = ExporterIFCUtils.GetLevelIdByHeight(exporterIFC, hostElem);
                  }
               }
            }

            // Check for a special parameter to determine whether the placement will be aligned at the top instead of at the bottom
            int intParam = 0;
            bool alignPlacementAtTop = false;
            if (ParameterUtil.GetIntValueFromElement(elem, "IfcAlignPlacementAtTheTop", out intParam) != null)
               alignPlacementAtTop = (intParam != 0) ? true : false;

            // todo: store.
            double bottomHeight = double.MaxValue;
            ElementId bottomLevelId = ElementId.InvalidElementId;
            if ((newLevelId == ElementId.InvalidElementId) || orientationTrf != null)
            {
               // if we have a trf, it might geometrically push the instance to a new level.  Check that case.
               // actually, we should ALWAYS check the bbox vs the settings
               newLevelId = ElementId.InvalidElementId;
               XYZ originToUse = XYZ.Zero;
               bool originIsValid = useOverrideOrigin;

               if (useOverrideOrigin)
               {
                  originToUse = overrideOrigin; 
               }
               else
               {
                  BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
                  if (bbox != null)
                  {
                     originToUse = alignPlacementAtTop ? bbox.Max : bbox.Min;
                     originIsValid = true;
                  }
                  else if (hostElem.Id != elemId)
                  {
                     bbox = hostElem.get_BoundingBox(null);
                     if (bbox != null)
                     {
                        originToUse = alignPlacementAtTop ? bbox.Max : bbox.Min;
                        originIsValid = true;
                     }
                  }
               }


               // The original heuristic here was that the origin determined the level containment based on exact location:
               // if the Z of the origin was higher than the current level but lower than the next level, it was contained
               // on that level.
               // However, in some places (e.g. Germany), the containment is thought to start just below the level, because floors
               // are placed before the level, not above.  So we have made a small modification so that anything within
               // 10cm of the 'next' level is on that level.

               double levelExtension = 10.0 / (12.0 * 2.54);
               double heightCoverage = 0.0;
               BoundingBoxXYZ elBbox = null;
               if (hostElem is AssemblyInstance)
               {
                  double maxX = double.MinValue;
                  double maxY = double.MinValue;
                  double maxZ = double.MinValue;
                  double minX = double.MaxValue;
                  double minY = double.MaxValue;
                  double minZ = double.MaxValue;

                  foreach (ElementId memberId in (hostElem as AssemblyInstance).GetMemberIds())
                  {
                     Element member = doc.GetElement(memberId);
                     BoundingBoxXYZ memberBox = member.get_BoundingBox(null);
                     if (elBbox == null)
                     {
                        elBbox = memberBox;
                        maxX = elBbox.Max.X;
                        maxY = elBbox.Max.Y;
                        maxZ = elBbox.Max.Z;
                        minX = elBbox.Min.X;
                        minY = elBbox.Min.Y;
                        minZ = elBbox.Min.Z;
                     }
                     else
                     {
                        if (memberBox.Max.X > maxX)
                           maxX = memberBox.Max.X;
                        if (memberBox.Max.Y > maxY)
                           maxY = memberBox.Max.Y;
                        if (memberBox.Max.Z > maxZ)
                           maxZ = memberBox.Max.Z;
                        if (memberBox.Min.X < minX)
                           minX = memberBox.Min.X;
                        if (memberBox.Min.Y < minY)
                           minY = memberBox.Min.Y;
                        if (memberBox.Min.Z < minZ)
                           minZ = memberBox.Min.Z;
                     }
                  }

                  elBbox.Max = new XYZ(maxX, maxY, maxZ);
                  elBbox.Min = new XYZ(minX, minY, minZ);
               }
               else
                  elBbox = hostElem.get_BoundingBox(null);

               if (elBbox != null)
               {
                  double elBboxHeight = elBbox.Max.Z - elBbox.Min.Z;
                  ElementId prevLevelId = ElementId.InvalidElementId;
                  //foreach (KeyValuePair<ElementId, IFCLevelInfo> levelInfoPair in levelInfos)
                  foreach (KeyValuePair<Tuple<double, ElementId>, IFCLevelInfo> sortedLevelInfoPair in sortedLevelInfos)
                  {
                     // the cache contains levels from all the exported documents
                     // if the export is performed for a linked document, filter the levels that are not from this document
                     if (ExporterCacheManager.ExportOptionsCache.ExportingLink)
                     {
                        Element levelElem = doc.GetElement(sortedLevelInfoPair.Key.Item2);
                        if (levelElem == null || !(levelElem is Level))
                           continue;
                     }

                     IFCLevelInfo levelInfo = sortedLevelInfoPair.Value;
                     //double startHeight = levelInfo.Elevation - levelExtension;
                     double startHeight = levelInfo.Elevation;
                     double height = levelInfo.DistanceToNextLevel;
                     bool useHeight = !MathUtil.IsAlmostZero(height);
                     double endHeight = startHeight + height;

                     // Keep the original logic to include object that is within the level extension below and the top box is above the level elevation
                     //if (originIsValid && ((originToUse[2] > (startHeight - MathUtil.Eps())) && (!useHeight || originToUse[2] < (endHeight - MathUtil.Eps()))))
                     if (originIsValid && (originToUse[2] < (levelInfo.Elevation - MathUtil.Eps())) && (originToUse[2] > (levelInfo.Elevation) - levelExtension + MathUtil.Eps())
                        && (elBbox.Max.Z > (startHeight - MathUtil.Eps())))
                     {
                        newLevelId = sortedLevelInfoPair.Key.Item2;
                        break;
                     }

                     // Clearcut case, if the object bbox is completely within the storey, or completely outside of the storey
                     if (elBbox.Min.Z > (startHeight - MathUtil.Eps()) && elBbox.Max.Z < (endHeight + MathUtil.Eps()))
                     {
                        newLevelId = sortedLevelInfoPair.Key.Item2;
                        break;
                     }
                     else if (elBbox.Max.Z < (startHeight - MathUtil.Eps()) || elBbox.Min.Z > (endHeight + MathUtil.Eps()))
                        continue;
                     // if the alignment for placement is set to the top (using parameter IfcAlignPlacementAtTheTop) and this is the top level, use the level. If not the top level continue to the next level
                     else if (alignPlacementAtTop && originToUse[2] > endHeight)
                     {
                        if (useHeight)
                           continue;
                        else
                        {
                           newLevelId = sortedLevelInfoPair.Key.Item2;
                           break;
                        }
                     }


                     double currLevelHeightCovMin = (elBbox.Min.Z < levelInfo.Elevation) ? levelInfo.Elevation : elBbox.Min.Z;
                     double currLevelHeightCovMax = 0.0;
                     if (useHeight)
                        currLevelHeightCovMax = (elBbox.Max.Z > (levelInfo.Elevation + height)) ? (levelInfo.Elevation + height) : elBbox.Max.Z;

                     double currHeightCov = (currLevelHeightCovMax - currLevelHeightCovMin) / height;

                     // Consider coverage comparison only after the first level
                     if (prevLevelId != ElementId.InvalidElementId)
                     {
                        // If the current height coverage is actually smaller than the previous one, use the previous level as the container instead
                        if (currHeightCov < heightCoverage && elBbox.Max.Z < (endHeight - MathUtil.Eps()) && prevLevelId != ElementId.InvalidElementId)
                        {
                           newLevelId = prevLevelId;
                           break;
                        }

                        // If the height coverage is more than the previous level coverage, we will use the level as the base level for the object
                        if (currHeightCov > heightCoverage && heightCoverage > 0)
                        {
                           newLevelId = sortedLevelInfoPair.Key.Item2;
                           break;
                        }
                     }

                     if (startHeight < (bottomHeight + MathUtil.Eps()))
                     {
                        bottomLevelId = sortedLevelInfoPair.Key.Item2;
                        bottomHeight = startHeight;
                     }

                     if (heightCoverage < currHeightCov)
                        prevLevelId = sortedLevelInfoPair.Key.Item2;
                     heightCoverage = currHeightCov;
                  }
               }
            }

            if (newLevelId == ElementId.InvalidElementId && bottomLevelId != ElementId.InvalidElementId)
               newLevelId = bottomLevelId;
         }

         LevelInfo = exporterIFC.GetLevelInfo(newLevelId);
         if (LevelInfo == null)
         {
            foreach (KeyValuePair<ElementId, IFCLevelInfo> levelInfoPair in levelInfos)
            {
               // the cache contains levels from all the exported documents
               // if the export is performed for a linked document, filter the levels that are not from this document
               if (ExporterCacheManager.ExportOptionsCache.ExportingLink)
               {
                  Element levelElem = doc.GetElement(levelInfoPair.Key);
                  if (levelElem == null || !(levelElem is Level))
                     continue;
               }
               LevelInfo = levelInfoPair.Value;
               break;
            }
            //LevelInfo = levelInfos.Values.First<IFCLevelInfo>();
         }

         double elevation = (LevelInfo != null) ? LevelInfo.Elevation : 0.0;
         IFCAnyHandle levelPlacement = (LevelInfo != null) ? LevelInfo.GetLocalPlacement() : null;

         IFCFile file = exporterIFC.GetFile();

         Transform trf = Transform.Identity;

         if (familyTrf != null)
         {
            XYZ origin, xDir, yDir, zDir;

            xDir = familyTrf.BasisX; yDir = familyTrf.BasisY; zDir = familyTrf.BasisZ;

            Transform origOffsetTrf = Transform.Identity;
            XYZ negLevelOrigin = new XYZ(0, 0, -elevation);
            origOffsetTrf.Origin = negLevelOrigin;

            Transform newTrf = origOffsetTrf * familyTrf;

            origin = newTrf.Origin;

            trf.BasisX = xDir; trf.BasisY = yDir; trf.BasisZ = zDir;
            trf = trf.Inverse;

            origin = UnitUtil.ScaleLength(origin);
            LocalPlacement = ExporterUtil.CreateLocalPlacement(file, levelPlacement, origin, zDir, xDir);
         }
         else if (orientationTrf != null)
         {
            XYZ origin, xDir, yDir, zDir;

            xDir = orientationTrf.BasisX; yDir = orientationTrf.BasisY; zDir = orientationTrf.BasisZ; origin = orientationTrf.Origin;

            XYZ levelOrigin = new XYZ(0, 0, elevation);
            origin = origin - levelOrigin;

            trf.BasisX = xDir; trf.BasisY = yDir; trf.BasisZ = zDir; trf.Origin = origin;
            trf = trf.Inverse;

            origin = UnitUtil.ScaleLength(origin);
            LocalPlacement = ExporterUtil.CreateLocalPlacement(file, levelPlacement, origin, zDir, xDir);
         }
         else
         {
            LocalPlacement = ExporterUtil.CreateLocalPlacement(file, levelPlacement, null, null, null);
         }

         Transform origOffsetTrf2 = Transform.Identity;
         XYZ negLevelOrigin2 = new XYZ(0, 0, -elevation);
         origOffsetTrf2.Origin = negLevelOrigin2;
         Transform newTrf2 = trf * origOffsetTrf2;

         ExporterIFC.PushTransform(newTrf2);
         Offset = elevation;
         LevelId = newLevelId;
      }

      #region IDisposable Members

      public void Dispose()
      {
         if (ExporterIFC != null)
            ExporterIFC.PopTransform();
      }

      #endregion
   }
}
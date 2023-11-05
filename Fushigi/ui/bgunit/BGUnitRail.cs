﻿using Fushigi.course;
using ImGuiNET;
using Microsoft.VisualBasic;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Fushigi.ui.widgets
{
    internal class BGUnitRail
    {
        public List<RailPoint> Points = new List<RailPoint>();

        public List<RailPoint> GetSelected() => Points.Where(x => x.IsSelected).ToList();

        public bool IsClosed = false;

        public bool IsInternal = false;

        public bool mouseDown = false;
        public bool transformStart = false;

        public bool IsSelected = false;
        public bool Visible = true;

        public uint Color_Default = 0xFFFFFFFF;
        public uint Color_SelectionEdit = ImGui.ColorConvertFloat4ToU32(new(0.84f, .437f, .437f, 1));
        public uint Color_SlopeError = 0xFF0000FF;

        private Vector3 mouseDownPos;

        public CourseUnit CourseUnit;

        public BGUnitRail(CourseUnit unit, CourseUnit.Rail rail)
        {
            CourseUnit = unit;

            this.Points.Clear();

            foreach (var pt in rail.mPoints)
                Points.Add(new RailPoint(pt.Value));

            IsClosed = rail.IsClosed;
            IsInternal = rail.IsInternal;
        }

        public void Reverse()
        {
            this.Points.Reverse();
        }

        public CourseUnit.Rail Save()
        {
            CourseUnit.Rail rail = new CourseUnit.Rail()
            {
                IsClosed = this.IsClosed,
                IsInternal = this.IsInternal,
                mPoints = new List<Vector3?>(),
            };

            rail.mPoints = new List<Vector3?>();
            foreach (var pt in this.Points)
                rail.mPoints.Add(pt.Position);

            return rail;
        }

        public void DeselectAll()
        {
            foreach (var point in Points)
                point.IsSelected = false;
        }

        public void SelectAll()
        {
            foreach (var point in Points)
                point.IsSelected = true;
        }

        public void InsertPoint(LevelViewport viewport, RailPoint point, int index)
        {
            this.Points.Insert(index, point);
            viewport.AddToUndo(new UnitRailPointAddUndo(this, point, index));
        }

        public void AddPoint(LevelViewport viewport, RailPoint point)
        {
            this.Points.Add(point);
            viewport.AddToUndo(new UnitRailPointAddUndo(this, point));
        }

        public void RemoveSelected(LevelViewport viewport)
        {
            var selected = this.GetSelected();
            if (selected.Count == 0)
                return;

            viewport.BeginUndoCollection();

            foreach (var point in selected)
                viewport.AddToUndo(new UnitRailPointDeleteUndo(this, point));

            viewport.EndUndoCollection();

            foreach (var point in selected)
                this.Points.Remove(point);
        }

        public void OnKeyDown(LevelViewport viewport)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Delete))
                RemoveSelected(viewport);
            if (IsSelected && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.A))
                SelectAll();
        }

        public void OnMouseDown(LevelViewport viewport)
        {
            //Line hit test
            if (!IsSelected && LevelViewport.HitTestLineLoopPoint(GetPoints(viewport), 4f,
                    ImGui.GetMousePos()))
            {
                viewport.SelectBGUnit(this);
                IsSelected = true;
            }

            if (!IsSelected)
                return;

            mouseDownPos = viewport.ScreenToWorld(ImGui.GetMousePos());

            var selected = GetSelected();

            if (ImGui.GetIO().KeyAlt && selected.Count == 1)
            {
                var index = this.Points.IndexOf(selected[0]);
                //Insert and add
                Vector3 posVec = viewport.ScreenToWorld(ImGui.GetMousePos());
                Vector3 pos = new(
                     MathF.Round(posVec.X, MidpointRounding.AwayFromZero),
                     MathF.Round(posVec.Y, MidpointRounding.AwayFromZero),
                     selected[0].Position.Z);

                DeselectAll();

                if (this.Points.Count - 1 == index) //is last point
                    InsertPoint(viewport, new RailPoint(pos) { IsSelected = true }, 0);
                else
                    InsertPoint(viewport, new RailPoint(pos) { IsSelected = true }, index + 1);
            }
            else if (ImGui.GetIO().KeyAlt && selected.Count == 0) //Add new point from last 
            {
                //Insert and add
                Vector3 posVec = viewport.ScreenToWorld(ImGui.GetMousePos());
                Vector3 pos = new(
                     MathF.Round(posVec.X, MidpointRounding.AwayFromZero),
                     MathF.Round(posVec.Y, MidpointRounding.AwayFromZero),
                     2);

                DeselectAll();

                AddPoint(viewport, new RailPoint(pos) { IsSelected = true });
            }
            else
                    {
                if (!ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                    DeselectAll();
            }

            for (int i = 0; i < Points.Count; i++)
            {
                Vector3 point = Points[i].Position;

                var pos2D = viewport.WorldToScreen(new(point.X, point.Y, point.Z));
                Vector2 pnt = new(pos2D.X, pos2D.Y);
                bool isHovered = (ImGui.GetMousePos() - pnt).Length() < 6.0f;

                if (isHovered)
                    Points[i].IsSelected = true;

                Points[i].PreviousPosition = point;
            }
            mouseDown = true;
        }

        private Vector2[] GetPoints(LevelViewport viewport)
        {
            Vector2[] points = new Vector2[Points.Count];
            for (int i = 0; i < Points.Count; i++)
            {
                Vector3 p = Points[i].Position;
                points[i] = viewport.WorldToScreen(new(p.X, p.Y, p.Z));
            }
            return points;
        }

        public void OnMouseUp(LevelViewport viewport)
        {
            mouseDown = false;
            transformStart = false;
        }

        public void OnSelecting(LevelViewport viewport)
        {
            if (!mouseDown) return;

            Vector3 posVec = viewport.ScreenToWorld(ImGui.GetMousePos());
            Vector3 diff = posVec - mouseDownPos;
            if (diff.X != 0 && diff.Y != 0 && !transformStart)
            {
                transformStart = true;
                //Store each selected point for undoing
                viewport.BeginUndoCollection();
                foreach (var point in this.GetSelected())
                    viewport.AddToUndo(new TransformUndo(point.Transform));
                viewport.EndUndoCollection();
            }

            for (int i = 0; i < Points.Count; i++)
            {
                if (transformStart && Points[i].IsSelected)
                {
                    diff.X = MathF.Round(diff.X, MidpointRounding.AwayFromZero);
                    diff.Y = MathF.Round(diff.Y, MidpointRounding.AwayFromZero);
                    posVec.Z = Points[i].Position.Z;
                    Points[i].Position = Points[i].PreviousPosition + diff;
                }
            }
        }

        public void Render(LevelViewport viewport, ImDrawListPtr mDrawList)
        {
            if (!this.Visible)
                return;

            if (ImGui.IsMouseClicked(0) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                OnMouseDown(viewport);
            if (ImGui.IsMouseReleased(0))
                OnMouseUp(viewport);

            if (viewport.mEditorState == LevelViewport.EditorState.Selecting)
                OnSelecting(viewport);

            OnKeyDown(viewport);

            for (int i = 0; i < Points.Count; i++)
            {
                Vector3 point = Points[i].Position;
                var pos2D = viewport.WorldToScreen(new(point.X, point.Y, point.Z));

                //Next pos 2D
                Vector2 nextPos2D = Vector2.Zero;
                if (i < Points.Count - 1) //is not last point
                {
                    nextPos2D = viewport.WorldToScreen(new(
                        Points[i + 1].Position.X,
                        Points[i + 1].Position.Y,
                        Points[i + 1].Position.Z));
                }
                else if (IsClosed) //last point to first if closed
                {
                    nextPos2D = viewport.WorldToScreen(new(
                       Points[0].Position.X,
                       Points[0].Position.Y,
                       Points[0].Position.Z));
                }
                else //last point but not closed, draw no line
                    continue;

                uint line_color = IsValidAngle(pos2D, nextPos2D) ? Color_Default : Color_SlopeError;
                if (this.IsSelected && line_color != Color_SlopeError)
                    line_color = Color_SelectionEdit;

                mDrawList.AddLine(pos2D, nextPos2D, line_color, 2.5f);

                if (IsSelected)
                {
                    //Arrow display
                    Vector3 next = (i < Points.Count - 1) ? Points[i + 1].Position : Points[0].Position;
                    Vector3 dist = (next - Points[i].Position);
                    var angleInRadian = MathF.Atan2(dist.Y, dist.X); //angle in radian
                    var rotation = Matrix4x4.CreateRotationZ(angleInRadian);

                    float width = 1f;

                    var line = Vector3.TransformNormal(new Vector3(0, width, 0), rotation);

                    Vector2[] arrow = new Vector2[2];
                    arrow[0] = viewport.WorldToScreen(Points[i].Position + (dist / 2f));
                    arrow[1] = viewport.WorldToScreen(Points[i].Position + (dist / 2f) + line);

                    float alpha = 0.5f;

                    mDrawList.AddLine(arrow[0], arrow[1], ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, alpha)), 2.5f);
                }
            }

            if (IsSelected)
            {
                for (int i = 0; i < Points.Count; i++)
                {
                    Vector3 point = Points[i].Position;
                    var pos2D = viewport.WorldToScreen(new(point.X, point.Y, point.Z));
                    Vector2 pnt = new(pos2D.X, pos2D.Y);

                    //Display point color
                    uint color = 0xFFFFFFFF;
                    if (Points[i].IsHovered || Points[i].IsSelected)
                        color = ImGui.ColorConvertFloat4ToU32(new(0.84f, .437f, .437f, 1));

                    mDrawList.AddCircleFilled(pos2D, 6.0f, color);

                    bool isHovered = (ImGui.GetMousePos() - pnt).Length() < 6.0f;
                    Points[i].IsHovered = isHovered;
                }
            }
        }

        private bool IsValidAngle(Vector2 point1, Vector2 point2)
        {
            var dist = point2 - point1;
            var angleInRadian = MathF.Atan2(dist.Y, dist.X); //angle in radian
            var angle = angleInRadian * (180.0f / (float)System.Math.PI); //to degrees

            //TODO improve check and simplify

            //The game supports 30 and 45 degree angle variants
            //Then ground (0) and wall (90)
            float[] validAngles = new float[]
            {
                0, -0,
                27, -27,
                45, -45,
                90, -90,
                135,-135,
                153,-153,
                180,-180,
            };

            return validAngles.Contains(MathF.Round(angle));
        }

        public class RailPoint
        {
            public Transform Transform = new Transform();

            public Vector3 Position
            {
                get { return Transform.Position; }
                set { Transform.Position = value; }
            }

            public bool IsSelected { get; set; }
            public bool IsHovered { get; set; }

            //For transforming
            public Vector3 PreviousPosition { get; set; }

            public RailPoint(Vector3 pos)
            {
                Position = pos;
            }
        }
    }
}
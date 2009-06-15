using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace Winterdom.VisualStudio.Extensions.Text {

   public class LineHighlight {
      public const string NAME = "LineHighlight";
      public const string CUR_LINE_TAG = "currentLine";
      private IAdornmentLayer layer;
      private IWpfTextView view;
      private IClassificationFormatMap formatMap;
      private IClassificationType formatType;
      private Brush fillBrush;
      private Pen borderPen;

      public LineHighlight(
            IWpfTextView view, IClassificationFormatMap formatMap, 
            IClassificationType formatType) {
         this.view = view;
         this.formatMap = formatMap;
         this.formatType = formatType;
         layer = view.GetAdornmentLayer(NAME);

         view.Caret.PositionChanged += OnCaretPositionChanged;
         view.ViewportWidthChanged += OnViewportWidthChanged;
         view.LayoutChanged += OnLayoutChanged;
         formatMap.ClassificationFormatMappingChanged += 
            OnClassificationFormatMappingChanged;

         CreateDrawingObjects();
      }

      void OnViewportWidthChanged(object sender, EventArgs e) {
         RedrawAdornments();
      }
      void OnClassificationFormatMappingChanged(object sender, EventArgs e) {
         // the user changed something in Fonts and Colors, so
         // recreate our addornments
         CreateDrawingObjects();
      }
      void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
         ITextViewLine newLine = GetLineByPos(e.NewPosition);
         ITextViewLine oldLine = GetLineByPos(e.OldPosition);
         if ( newLine != oldLine ) {
            layer.RemoveAdornmentsByTag(CUR_LINE_TAG);
            this.CreateVisuals(newLine);
         }
      }
      void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
         SnapshotPoint caret = view.Caret.Position.BufferPosition;
         foreach ( var line in e.NewOrReformattedLines ) {
            if ( line.ContainsBufferPosition(caret) ) {
               this.CreateVisuals(line);
               break;
            }
         }
      }

      private void CreateDrawingObjects() {
         Brush oldFillBrush = fillBrush;
         Pen oldBorderPen = borderPen;

         // this gets the color settings configured by the
         // user in Fonts and Colors (or the default in out
         // classification type).
         TextFormattingRunProperties format =
            formatMap.GetTextProperties(formatType);

         fillBrush = format.BackgroundBrush;
         Brush penBrush = format.ForegroundBrush;
         borderPen = new Pen(penBrush, 0.5);
         borderPen.Freeze();
         RedrawAdornments();
      }
      private void RedrawAdornments() {
         layer.RemoveAdornmentsByTag(CUR_LINE_TAG);
         var caret = view.Caret.Position;
         ITextViewLine line = GetLineByPos(caret);
         this.CreateVisuals(line);
      }
      private ITextViewLine GetLineByPos(CaretPosition pos) {
         return view.GetTextViewLineContainingBufferPosition(pos.BufferPosition);
      }
      private void CreateVisuals(ITextViewLine line) {
         IWpfTextViewLineCollection textViewLines = view.TextViewLines;
         if ( textViewLines == null )
            return; // not ready yet.
         int start = line.Start;
         int end = line.End;

         SnapshotSpan span = 
            new SnapshotSpan(view.TextSnapshot, Span.FromBounds(start, end));
         Geometry g = textViewLines.GetMarkerGeometry(span);
         if ( g != null ) {
            // adjust the adornment location
            double maxRight = Math.Max(view.ViewportRight-2, g.Bounds.Right);
            Rect nr = new Rect(
               g.Bounds.TopLeft, 
               new Point(maxRight, g.Bounds.Bottom)
            );
            g = new RectangleGeometry(nr, 1.1, 1.1);

            GeometryDrawing drawing = new GeometryDrawing(fillBrush, borderPen, g);
            drawing.Freeze();
            DrawingImage drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            Image image = new Image();
            image.Source = drawingImage;
            //Align the image with the top of the bounds of the text geometry
            Canvas.SetLeft(image, g.Bounds.Left);
            Canvas.SetTop(image, g.Bounds.Top);

            layer.AddAdornment(
               AdornmentPositioningBehavior.TextRelative, span, 
               CUR_LINE_TAG, image, null
            );
         }
      }
   }
}

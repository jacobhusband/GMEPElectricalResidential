﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace GMEPElectricalResidential
{
  public class Commands
  {
    private static LoadCalculationForm _loadCalculationForm;
    private static SINGLE_LINE_DIAGRAM _sld;

    [CommandMethod("SLD")]
    public void SLD()
    {
      if (_sld == null || _sld.IsDisposed)
      {
        _sld = new SINGLE_LINE_DIAGRAM();
      }

      _sld.Show();
      _sld.BringToFront();
    }

    [CommandMethod("LoadCalculation")]
    public void LoadCalculation()
    {
      if (_loadCalculationForm == null || _loadCalculationForm.IsDisposed)
      {
        _loadCalculationForm = new LoadCalculationForm();
      }

      _loadCalculationForm.Show();
      _loadCalculationForm.BringToFront();
    }

    [CommandMethod("GetObjectData")]
    public static void GetObjectData()
    {
      var data = new ObjectData();

      Autodesk.AutoCAD.EditorInput.Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
      Autodesk.AutoCAD.EditorInput.PromptSelectionResult selectionResult = ed.GetSelection();
      if (selectionResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
      {
        Autodesk.AutoCAD.EditorInput.SelectionSet selectionSet = selectionResult.Value;
        Autodesk.AutoCAD.EditorInput.PromptPointOptions originOptions = new Autodesk.AutoCAD.EditorInput.PromptPointOptions("Select an origin point: ");
        Autodesk.AutoCAD.EditorInput.PromptPointResult originResult = ed.GetPoint(originOptions);
        if (originResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
        {
          Point3d origin = originResult.Value;

          foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId objectId in selectionSet.GetObjectIds())
          {
            using (Transaction transaction = objectId.Database.TransactionManager.StartTransaction())
            {
              Autodesk.AutoCAD.DatabaseServices.DBObject obj = transaction.GetObject(objectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);

              if (obj is Autodesk.AutoCAD.DatabaseServices.Polyline)
              {
                data = HandlePolyline(obj as Autodesk.AutoCAD.DatabaseServices.Polyline, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.Arc)
              {
                data = HandleArc(obj as Autodesk.AutoCAD.DatabaseServices.Arc, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.Circle)
              {
                data = HandleCircle(obj as Autodesk.AutoCAD.DatabaseServices.Circle, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.Ellipse)
              {
                data = HandleEllipse(obj as Autodesk.AutoCAD.DatabaseServices.Ellipse, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.MText)
              {
                data = HandleMText(obj as Autodesk.AutoCAD.DatabaseServices.MText, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.Solid)
              {
                data = HandleSolid(obj as Autodesk.AutoCAD.DatabaseServices.Solid, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.Line)
              {
                data = HandleLine(obj as Autodesk.AutoCAD.DatabaseServices.Line, data, origin);
              }
              else if (obj is Autodesk.AutoCAD.DatabaseServices.DBText)
              {
                data = HandleText(obj as Autodesk.AutoCAD.DatabaseServices.DBText, data, origin);
              }

              transaction.Commit();
            }
          }
        }
      }

      SaveDataToJsonFile(data, "data.json");
    }

    [CommandMethod("CreateObjectFromData")]
    public static void CreateObjectFromData()
    {
      string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
      string filePath = Path.Combine(desktopPath, "data.json");
      string jsonData = File.ReadAllText(filePath);

      ObjectData objectData = JsonConvert.DeserializeObject<ObjectData>(jsonData);

      Document acDoc = Application.DocumentManager.MdiActiveDocument;
      Database acCurDb = acDoc.Database;
      Editor ed = acDoc.Editor;

      PromptPointOptions ppo = new PromptPointOptions("Select a point: ");
      PromptPointResult ppr = ed.GetPoint(ppo);

      if (ppr.Status != PromptStatus.OK) return;

      Point3d basePoint = ppr.Value;

      using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
      {
        BlockTable acBlkTbl;
        acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;
        BlockTableRecord acBlkTblRec;
        acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.PaperSpace], OpenMode.ForWrite) as BlockTableRecord;

        foreach (var polyline in objectData.Polylines)
        {
          basePoint = CreatePolyline(basePoint, acTrans, acBlkTblRec, polyline);
        }

        foreach (var line in objectData.Lines)
        {
          basePoint = CreateLine(basePoint, acTrans, acBlkTblRec, line);
        }

        foreach (var arc in objectData.Arcs)
        {
          basePoint = CreateArc(basePoint, acTrans, acBlkTblRec, arc);
        }

        foreach (var circle in objectData.Circles)
        {
          basePoint = CreateCircle(basePoint, acTrans, acBlkTblRec, circle);
        }

        foreach (var ellipse in objectData.Ellipses)
        {
          basePoint = CreateEllipse(basePoint, acTrans, acBlkTblRec, ellipse);
        }

        foreach (var mText in objectData.MTexts)
        {
          basePoint = CreateMText(basePoint, acTrans, acBlkTblRec, mText);
        }

        foreach (var text in objectData.Texts)
        {
          basePoint = CreateText(basePoint, acTrans, acBlkTblRec, text);
        }

        foreach (var solid in objectData.Solids)
        {
          basePoint = CreateSolid(basePoint, acTrans, acBlkTblRec, solid);
        }

        acTrans.Commit();
      }
    }

    private static Point3d CreateText(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, TextData text)
    {
      DBText dbText = new DBText();
      dbText.Layer = text.Layer;
      dbText.TextString = text.Contents;
      dbText.Position = new Point3d(basePoint.X + text.Location.X, basePoint.Y + text.Location.Y, basePoint.Z + text.Location.Z);
      dbText.Height = text.Height;
      dbText.Rotation = text.Rotation;
      dbText.WidthFactor = text.LineSpaceDistance;
      SetTextStyleByName(dbText, text.Style);

      acBlkTblRec.AppendEntity(dbText);
      acTrans.AddNewlyCreatedDBObject(dbText, true);
      return basePoint;
    }

    private static void SetTextStyleByName(Entity textEntity, string styleName)
    {
      if (!(textEntity is MText || textEntity is DBText))
      {
        throw new ArgumentException("The textEntity must be of type MText or DBText.");
      }

      Database db = HostApplicationServices.WorkingDatabase;
      using (Transaction tr = db.TransactionManager.StartTransaction())
      {
        TextStyleTable textStyleTable = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
        if (textStyleTable.Has(styleName))
        {
          TextStyleTableRecord textStyle = tr.GetObject(textStyleTable[styleName], OpenMode.ForRead) as TextStyleTableRecord;
          if (textEntity is MText mTextEntity)
          {
            mTextEntity.TextStyleId = textStyle.ObjectId;
          }
          else if (textEntity is DBText dbTextEntity)
          {
            dbTextEntity.TextStyleId = textStyle.ObjectId;
          }
        }
        tr.Commit();
      }
    }

    private static Point3d CreateSolid(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, SolidData solidData)
    {
      Solid solid = new Solid();
      solid.Layer = solidData.Layer;
      for (short i = 0; i < solidData.Vertices.Count; i++)
      {
        SimpleVector3d vector = solidData.Vertices[i];
        solid.SetPointAt(i, new Point3d(basePoint.X + vector.X, basePoint.Y + vector.Y, basePoint.Z + vector.Z));
      }

      acBlkTblRec.AppendEntity(solid);
      acTrans.AddNewlyCreatedDBObject(solid, true);
      return basePoint;
    }

    private static Point3d CreateMText(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, MTextData mTextData)
    {
      MText mText = new MText();
      mText.Layer = mTextData.Layer;
      SetMTextStyleByName(mText, mTextData.Style);
      mText.Attachment = (AttachmentPoint)Enum.Parse(typeof(AttachmentPoint), mTextData.Justification);
      mText.Contents = mTextData.Contents;
      mText.Location = new Point3d(basePoint.X + mTextData.Location.X, basePoint.Y + mTextData.Location.Y, basePoint.Z + mTextData.Location.Z);
      mText.LineSpaceDistance = mTextData.LineSpaceDistance;
      mText.Height = mTextData.Height;
      mText.Width = mTextData.Width;
      mText.Rotation = mTextData.Rotation;

      acBlkTblRec.AppendEntity(mText);
      acTrans.AddNewlyCreatedDBObject(mText, true);
      return basePoint;
    }

    private static Point3d CreateEllipse(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, EllipseData ellipseData)
    {
      Ellipse ellipse = new Ellipse();
      ellipse.Layer = ellipseData.Layer;
      Point3d center = new Point3d(basePoint.X + ellipseData.Center.X, basePoint.Y + ellipseData.Center.Y, basePoint.Z + ellipseData.Center.Z);
      Vector3d majorAxis = new Vector3d(ellipseData.MajorAxis.X, ellipseData.MajorAxis.Y, ellipseData.MajorAxis.Z);
      double radiusRatio = ellipseData.RadiusRatio();
      double startAngle = ellipseData.StartAngle;
      double endAngle = ellipseData.EndAngle;
      Vector3d unitNormal = new Vector3d(0, 0, 1);

      ellipse.Set(center, unitNormal, majorAxis, radiusRatio, startAngle, endAngle);

      acBlkTblRec.AppendEntity(ellipse);
      acTrans.AddNewlyCreatedDBObject(ellipse, true);
      return basePoint;
    }

    private static Point3d CreateCircle(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, CircleData circleData)
    {
      Circle circle = new Circle();
      circle.Layer = circleData.Layer;
      circle.Center = new Point3d(basePoint.X + circleData.Center.X, basePoint.Y + circleData.Center.Y, basePoint.Z + circleData.Center.Z);
      circle.Radius = circleData.Radius;

      acBlkTblRec.AppendEntity(circle);
      acTrans.AddNewlyCreatedDBObject(circle, true);
      return basePoint;
    }

    private static Point3d CreateArc(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, ArcData arcData)
    {
      Arc arc = new Arc();
      arc.Layer = arcData.Layer;
      arc.Center = new Point3d(basePoint.X + arcData.Center.X, basePoint.Y + arcData.Center.Y, basePoint.Z + arcData.Center.Z);
      arc.Radius = arcData.Radius;
      arc.StartAngle = arcData.StartAngle;
      arc.EndAngle = arcData.EndAngle;

      acBlkTblRec.AppendEntity(arc);
      acTrans.AddNewlyCreatedDBObject(arc, true);
      return basePoint;
    }

    private static Point3d CreateLine(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, LineData lineData)
    {
      Line line = new Line();
      line.Layer = lineData.Layer;
      line.StartPoint = new Point3d(basePoint.X + lineData.StartPoint.X, basePoint.Y + lineData.StartPoint.Y, basePoint.Z + lineData.StartPoint.Z);
      line.EndPoint = new Point3d(basePoint.X + lineData.EndPoint.X, basePoint.Y + lineData.EndPoint.Y, basePoint.Z + lineData.EndPoint.Z);

      acBlkTblRec.AppendEntity(line);
      acTrans.AddNewlyCreatedDBObject(line, true);
      return basePoint;
    }

    private static Point3d CreatePolyline(Point3d basePoint, Transaction acTrans, BlockTableRecord acBlkTblRec, PolylineData polylineData)
    {
      Polyline polyline = new Polyline();
      polyline.Layer = polylineData.Layer;
      polyline.Linetype = polylineData.LineType;
      polyline.Closed = polylineData.Closed;

      for (int i = 0; i < polylineData.Vectors.Count; i++)
      {
        SimpleVector3d vector = polylineData.Vectors[i];
        polyline.AddVertexAt(i, new Point2d(basePoint.X + vector.X, basePoint.Y + vector.Y), 0, 0, 0);
      }

      acBlkTblRec.AppendEntity(polyline);
      acTrans.AddNewlyCreatedDBObject(polyline, true);
      return basePoint;
    }

    private static ObjectData HandlePolyline(Polyline polyline, ObjectData data, Point3d origin)
    {
      var polylineData = new PolylineData
      {
        Layer = polyline.Layer,
        Vectors = new List<SimpleVector3d>(),
        LineType = polyline.Linetype,
        Closed = polyline.Closed,
      };

      for (int i = 0; i < polyline.NumberOfVertices; i++)
      {
        Point3d point = polyline.GetPoint3dAt(i);
        Vector3d vector = point - origin;
        polylineData.Vectors.Add(new SimpleVector3d { X = vector.X, Y = vector.Y, Z = vector.Z });
      }

      data.Polylines.Add(polylineData);

      return data;
    }

    private static ObjectData HandleArc(Arc arc, ObjectData data, Point3d origin)
    {
      var arcData = new ArcData
      {
        Layer = arc.Layer,
        Center = new SimpleVector3d
        {
          X = arc.Center.X - origin.X,
          Y = arc.Center.Y - origin.Y,
          Z = arc.Center.Z - origin.Z
        },
        Radius = arc.Radius,
        StartAngle = arc.StartAngle,
        EndAngle = arc.EndAngle,
      };

      data.Arcs.Add(arcData);

      return data;
    }

    private static ObjectData HandleCircle(Circle circle, ObjectData data, Point3d origin)
    {
      var circleData = new CircleData
      {
        Layer = circle.Layer,
        Center = new SimpleVector3d
        {
          X = circle.Center.X - origin.X,
          Y = circle.Center.Y - origin.Y,
          Z = circle.Center.Z - origin.Z
        },
        Radius = circle.Radius,
      };

      data.Circles.Add(circleData);

      return data;
    }

    private static ObjectData HandleEllipse(Ellipse ellipse, ObjectData data, Point3d origin)
    {
      var ellipseData = new EllipseData
      {
        Layer = ellipse.Layer,
        UnitNormal = new SimpleVector3d
        {
          X = ellipse.Normal.X,
          Y = ellipse.Normal.Y,
          Z = ellipse.Normal.Z
        },
        Center = new SimpleVector3d
        {
          X = ellipse.Center.X - origin.X,
          Y = ellipse.Center.Y - origin.Y,
          Z = ellipse.Center.Z - origin.Z
        },
        MajorAxis = new SimpleVector3d
        {
          X = ellipse.MajorAxis.X,
          Y = ellipse.MajorAxis.Y,
          Z = ellipse.MajorAxis.Z
        },
        MajorRadius = ellipse.MajorRadius,
        MinorRadius = ellipse.MinorRadius,
        StartAngle = ellipse.StartAngle,
        EndAngle = ellipse.EndAngle,
      };

      data.Ellipses.Add(ellipseData);

      return data;
    }

    private static ObjectData HandleMText(MText mText, ObjectData data, Point3d origin)
    {
      var mTextData = new MTextData
      {
        Layer = mText.Layer,
        Style = mText.TextStyleName,
        Justification = mText.Attachment.ToString(),
        Contents = mText.Contents,
        Location = new SimpleVector3d
        {
          X = mText.Location.X - origin.X,
          Y = mText.Location.Y - origin.Y,
          Z = mText.Location.Z - origin.Z
        },
        LineSpaceDistance = mText.LineSpaceDistance,
        Height = mText.ActualHeight,
        Width = mText.ActualWidth,
        Rotation = mText.Rotation,
      };

      data.MTexts.Add(mTextData);

      return data;
    }

    private static ObjectData HandleSolid(Solid solid, ObjectData data, Point3d origin)
    {
      var solidData = new SolidData
      {
        Layer = solid.Layer,
        Vertices = new List<SimpleVector3d>(),
      };

      for (short i = 0; i < 4; i++)
      {
        Point3d point = solid.GetPointAt(i);
        Vector3d vector = point - origin;
        solidData.Vertices.Add(new SimpleVector3d { X = vector.X, Y = vector.Y, Z = vector.Z });
      }

      data.Solids.Add(solidData);

      return data;
    }

    private static ObjectData HandleLine(Line line, ObjectData data, Point3d origin)
    {
      var lineData = new LineData
      {
        Layer = line.Layer,
        StartPoint = new SimpleVector3d
        {
          X = line.StartPoint.X - origin.X,
          Y = line.StartPoint.Y - origin.Y,
          Z = line.StartPoint.Z - origin.Z
        },
        EndPoint = new SimpleVector3d
        {
          X = line.EndPoint.X - origin.X,
          Y = line.EndPoint.Y - origin.Y,
          Z = line.EndPoint.Z - origin.Z
        },
      };

      data.Lines.Add(lineData);

      return data;
    }

    private static ObjectData HandleText(DBText text, ObjectData data, Point3d origin)
    {
      var textData = new TextData
      {
        Layer = text.Layer,
        Style = text.TextStyleName,
        Contents = text.TextString,
        Location = new SimpleVector3d
        {
          X = text.Position.X - origin.X,
          Y = text.Position.Y - origin.Y,
          Z = text.Position.Z - origin.Z
        },
        LineSpaceDistance = text.WidthFactor,
        Height = text.Height,
        Rotation = text.Rotation,
        AlignmentPoint = new SimpleVector3d
        {
          X = text.AlignmentPoint.X - origin.X,
          Y = text.AlignmentPoint.Y - origin.Y,
          Z = text.AlignmentPoint.Z - origin.Z
        },
        IsMirroredInX = text.IsMirroredInX,
        IsMirroredInY = text.IsMirroredInY
      };

      data.Texts.Add(textData);

      return data;
    }

    public static void SaveDataToJsonFile(object data, string fileName)
    {
      string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
      string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
      string fullPath = Path.Combine(desktopPath, fileName);
      File.WriteAllText(fullPath, jsonData);
    }
  }

  internal class ObjectData
  {
    public List<PolylineData> Polylines { get; set; }
    public List<LineData> Lines { get; set; }
    public List<ArcData> Arcs { get; set; }
    public List<CircleData> Circles { get; set; }
    public List<EllipseData> Ellipses { get; set; }
    public List<MTextData> MTexts { get; set; }
    public List<TextData> Texts { get; set; }
    public List<SolidData> Solids { get; set; }

    public ObjectData()
    {
      Polylines = new List<PolylineData>();
      Lines = new List<LineData>();
      Arcs = new List<ArcData>();
      Circles = new List<CircleData>();
      Ellipses = new List<EllipseData>();
      MTexts = new List<MTextData>();
      Solids = new List<SolidData>();
    }
  }

  internal class TextData : BaseData
  {
    public string Style { get; set; }
    public string Justification { get; set; }
    public string Contents { get; set; }
    public SimpleVector3d Location { get; set; }
    public double LineSpaceDistance { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public SimpleVector3d AlignmentPoint { get; set; }
    public bool IsMirroredInX { get; set; }
    public bool IsMirroredInY { get; set; }
  }

  internal class PolylineData : BaseData
  {
    public List<SimpleVector3d> Vectors { get; set; }
    public string LineType { get; set; }
    public bool Closed { get; set; }
  }

  internal class LineData : BaseData
  {
    public SimpleVector3d StartPoint { get; set; }
    public SimpleVector3d EndPoint { get; set; }
  }

  internal class ArcData : BaseData
  {
    public SimpleVector3d Center { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; }
    public double EndAngle { get; set; }
  }

  internal class CircleData : BaseData
  {
    public SimpleVector3d Center { get; set; }
    public double Radius { get; set; }
  }

  internal class EllipseData : BaseData
  {
    public SimpleVector3d UnitNormal { get; set; }
    public SimpleVector3d Center { get; set; }
    public SimpleVector3d MajorAxis { get; set; }
    public double MajorRadius { get; set; }
    public double MinorRadius { get; set; }
    public double StartAngle { get; set; }
    public double EndAngle { get; set; }

    public double RadiusRatio()
    {
      if (MinorRadius != 0 && MajorRadius != 0)
      {
        return MinorRadius / MajorRadius;
      }
      else
      {
        return 0;
      }
    }
  }

  internal class MTextData : BaseData
  {
    public string Style { get; set; }
    public string Justification { get; set; }
    public string Contents { get; set; }
    public SimpleVector3d Location { get; set; }
    public double LineSpaceDistance { get; set; }
    public double Height { get; set; }
    public double Width { get; set; }
    public double Rotation { get; set; }
  }

  internal class SolidData : BaseData
  {
    public List<SimpleVector3d> Vertices { get; set; }
  }

  public class BaseData
  {
    public string Layer { get; set; }
  }

  public class SimpleVector3d
  {
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
  }

  public class BaseDataConverter : JsonConverter
  {
    public override bool CanConvert(Type objectType)
    {
      return (objectType == typeof(BaseData));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      JObject jo = JObject.Load(reader);
      switch (jo["Type"].Value<string>())
      {
        case "PolylineData":
          return jo.ToObject<PolylineData>(serializer);

        case "LineData":
          return jo.ToObject<LineData>(serializer);

        case "ArcData":
          return jo.ToObject<ArcData>(serializer);

        case "CircleData":
          return jo.ToObject<CircleData>(serializer);

        case "EllipseData":
          return jo.ToObject<EllipseData>(serializer);

        case "MTextData":
          return jo.ToObject<MTextData>(serializer);

        case "SolidData":
          return jo.ToObject<SolidData>(serializer);

        default:
          throw new System.Exception("Type property not found!");
      }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      if (value is PolylineData polylineData)
      {
        serializer.Serialize(writer, polylineData);
      }
      else if (value is LineData lineData)
      {
        serializer.Serialize(writer, lineData);
      }
      else if (value is ArcData arcData)
      {
        serializer.Serialize(writer, arcData);
      }
      else if (value is CircleData circleData)
      {
        serializer.Serialize(writer, circleData);
      }
      else if (value is EllipseData ellipseData)
      {
        serializer.Serialize(writer, ellipseData);
      }
      else if (value is MTextData mTextData)
      {
        serializer.Serialize(writer, mTextData);
      }
      else if (value is SolidData solidData)
      {
        serializer.Serialize(writer, solidData);
      }
      else
      {
        throw new System.Exception("Unknown type!");
      }
    }
  }
}
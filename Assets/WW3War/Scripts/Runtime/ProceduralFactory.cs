using System.Collections.Generic;
using UnityEngine;
using WW3War.Core;

namespace WW3War
{
    public static class ProceduralFactory
    {
        public static readonly Color Blue = new Color(.23f, .66f, .76f);
        public static readonly Color Red = new Color(.83f, .35f, .25f);
        public static readonly Color Sand = new Color(.53f, .52f, .40f);
        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        public static Material Material(Color color)
        {
            if (Materials.TryGetValue(color, out var material)) return material;
            var shader = Resources.Load<Shader>("FieldSurface");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { color = color };
            Materials.Add(color, material);
            return material;
        }

        public static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Material(color);
            var collider = obj.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            return obj;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
            => Shape(parent, name, PrimitiveType.Cube, pos, scale, color);

        public static GameObject Ring(Transform parent, Color color, float radius, float height = .06f)
        {
            var root = new GameObject("Tactical ring"); root.transform.SetParent(parent, false);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                var segment = Box(root.transform, "Arc", new Vector3(Mathf.Sin(angle)*radius,height,Mathf.Cos(angle)*radius), new Vector3(.12f,.07f,radius*.20f), color);
                segment.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 90, 0);
            }
            return root;
        }

        public static void UnitModel(Transform parent, UnitKind kind, int team)
        {
            Color color = team == 1 ? new Color(.29f,.43f,.40f) : new Color(.53f,.40f,.29f);
            Color trim = team == 1 ? Blue : Red;
            Color dark = new Color(.12f,.16f,.16f);
            Shape(parent, "Ground shadow", PrimitiveType.Cylinder, new Vector3(0,.025f,0), new Vector3(4.3f,.015f,4.3f), new Color(.21f,.24f,.20f));
            if (kind == UnitKind.Infantry)
            {
                for (int i=0;i<6;i++)
                {
                    var p = new Vector3((i%3-1)*1.35f,0,(i/3-.5f)*1.7f);
                    Shape(parent,"Infantry torso",PrimitiveType.Capsule,p+Vector3.up*.95f,new Vector3(.48f,.66f,.48f),color);
                    Shape(parent,"Helmet",PrimitiveType.Sphere,p+Vector3.up*1.68f,Vector3.one*.51f,trim);
                    Box(parent,"Rifle",p+new Vector3(.28f,1.1f,.49f),new Vector3(.12f,.13f,1.0f),dark);
                    Box(parent,"Pack",p+new Vector3(0,1.03f,-.31f),new Vector3(.46f,.6f,.24f),dark);
                }
            }
            else
            {
                Box(parent,"Track left",new Vector3(-1.15f,.5f,0),new Vector3(.62f,.85f,3.8f),dark);
                Box(parent,"Track right",new Vector3(1.15f,.5f,0),new Vector3(.62f,.85f,3.8f),dark);
                Box(parent,"Armored chassis",new Vector3(0,.92f,0),new Vector3(2.35f,.75f,3.65f),color);
                var turret=Box(parent,"Turret",new Vector3(0,1.56f,-.15f),new Vector3(1.55f,.75f,1.75f),color*.86f);
                var barrel=Box(turret.transform,"Main gun",new Vector3(0,.04f,1.15f),new Vector3(.15f,.18f,1.7f),dark);
                if(kind==UnitKind.Artillery) barrel.transform.localRotation=Quaternion.Euler(-16,0,0);
                Box(parent,"Identification panel",new Vector3(0,1.96f,-.2f),new Vector3(.8f,.08f,.6f),trim);
                Box(parent,"Antenna",new Vector3(.7f,2.08f,-1.1f),new Vector3(.035f,1.8f,.035f),dark);
            }
        }

        public static void Terrain(Transform root, List<Vector3> covers)
        {
            Box(root,"Battlefield",new Vector3(0,-.3f,0),new Vector3(154,.5f,114),Sand);
            Box(root,"Highway",new Vector3(0,.006f,0),new Vector3(148,.06f,9),new Color(.27f,.29f,.27f));
            Box(root,"Access road",new Vector3(9,.008f,0),new Vector3(6,.065f,105),new Color(.34f,.35f,.30f));
            for(int i=-14;i<15;i++) Box(root,"Road marking",new Vector3(i*5,.052f,0),new Vector3(2,.025f,.15f),new Color(.71f,.70f,.57f));
            for(int x=-70;x<=70;x+=10) Box(root,"Map grid",new Vector3(x,-.02f,0),new Vector3(.07f,.065f,104),new Color(.57f,.56f,.44f));
            for(int z=-50;z<=50;z+=10) Box(root,"Map grid",new Vector3(0,-.02f,z),new Vector3(144,.065f,.07f),new Color(.57f,.56f,.44f));
            Vector3[] coverPositions={new Vector3(-22,0,-16),new Vector3(-18,0,18),new Vector3(22,0,-18),new Vector3(20,0,18),new Vector3(0,0,9),new Vector3(0,0,-10)};
            foreach(var p in coverPositions)
            {
                covers.Add(p);
                for(int i=0;i<5;i++) Box(root,"Sandbag cover",p+new Vector3((i-2)*1.1f,.48f,0),new Vector3(1,.95f,.65f),new Color(.65f,.61f,.43f));
            }
            var random=new System.Random(41);
            for(int i=0;i<20;i++)
            {
                float x=(float)random.NextDouble()*130-65;
                float z=(i%2==0?-1:1)*(35+(float)random.NextDouble()*13);
                float height=2+(float)random.NextDouble()*7;
                var p=new Vector3(x,height*.5f,z);
                Box(root,"Distant warehouse",p,new Vector3(5,height,7),new Color(.48f,.47f,.39f));
                Box(root,"Roof",p+Vector3.up*(height*.5f+.2f),new Vector3(5.3f,.4f,7.3f),new Color(.31f,.34f,.31f));
            }
            Shape(root,"Relay platform",PrimitiveType.Cylinder,new Vector3(0,.1f,0),new Vector3(8,.1f,8),new Color(.42f,.43f,.37f));
            Box(root,"Relay mast",new Vector3(0,3.5f,0),new Vector3(.28f,7,.28f),new Color(.73f,.70f,.58f));
            for(int y=2;y<7;y+=2) Box(root,"Relay antenna",new Vector3(0,y,0),new Vector3(2,.1f,.1f),new Color(.28f,.32f,.29f));
            Ring(root,new Color(.9f,.74f,.36f),8,.12f);
        }
    }
}

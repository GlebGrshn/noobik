using UnityEngine;
using UnityEngine.UI;

namespace Nubik
{
    /// <summary>Small resolution-independent game icons, drawn without font glyphs or external textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiGlyph : MaskableGraphic
    {
        public enum Kind { Coin, Bag, Down, Shovel, Helmet, Shell, Crystal, Gear, Key, Lock, Heart, Jet, Radar, Cross, Gem, Menu }
        private Kind glyph;
        public Kind kind
        {
            get => glyph;
            set { if (glyph == value) return; glyph = value; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (kind)
            {
                case Kind.Coin:
                    Ring(mesh, 0.5f, 0.5f, 0.39f, 0.29f); Line(mesh, .5f, .29f, .5f, .71f, .08f); break;
                case Kind.Bag:
                    Box(mesh, .2f, .17f, .8f, .72f); Ring(mesh, .5f, .71f, .2f, .12f);
                    Line(mesh, .28f, .35f, .72f, .35f, .055f); break;
                case Kind.Down:
                    Line(mesh, .5f, .84f, .5f, .2f, .085f);
                    Line(mesh, .23f, .46f, .5f, .19f, .085f); Line(mesh, .77f, .46f, .5f, .19f, .085f); break;
                case Kind.Shovel:
                    Line(mesh, .36f, .40f, .76f, .80f, .1f);
                    Line(mesh, .64f, .90f, .88f, .66f, .1f);
                    // Spade blade pointing down-left from the handle.
                    var left = new Vector2(.26f, .50f); var right = new Vector2(.46f, .30f);
                    var lowLeft = new Vector2(.12f, .36f); var lowRight = new Vector2(.32f, .16f); var tip = new Vector2(.12f, .16f);
                    Tri(mesh, left, right, lowRight); Tri(mesh, left, lowRight, tip); Tri(mesh, left, tip, lowLeft); break;
                case Kind.Helmet:
                    Disc(mesh, .5f, .43f, .34f); Box(mesh, .1f, .2f, .9f, .3f);
                    Box(mesh, .43f, .58f, .57f, .88f); break;
                case Kind.Shell:
                    for (int i = 0; i < 5; i++) Line(mesh,.5f,.15f,.15f+i*.175f,.65f+(.5f-Mathf.Abs(i-2)*.2f)*.3f,.095f);
                    break;
                case Kind.Crystal:
                    Tri(mesh,new Vector2(.16f,.55f),new Vector2(.5f,.08f),new Vector2(.84f,.55f));
                    Tri(mesh,new Vector2(.16f,.55f),new Vector2(.84f,.55f),new Vector2(.5f,.93f)); break;
                case Kind.Gear:
                    Ring(mesh,.5f,.5f,.3f,.16f);
                    for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; Line(mesh,.5f+Mathf.Cos(a)*.24f,.5f+Mathf.Sin(a)*.24f,.5f+Mathf.Cos(a)*.43f,.5f+Mathf.Sin(a)*.43f,.12f); } break;
                case Kind.Key:
                    Ring(mesh,.32f,.7f,.22f,.12f); Line(mesh,.43f,.56f,.78f,.2f,.085f);
                    Line(mesh,.67f,.31f,.8f,.44f,.085f); break;
                case Kind.Heart:
                    Disc(mesh, .33f, .64f, .2f); Disc(mesh, .67f, .64f, .2f);
                    Tri(mesh, new Vector2(.14f, .6f), new Vector2(.86f, .6f), new Vector2(.5f, .12f)); break;
                case Kind.Jet:
                    Box(mesh, .2f, .34f, .42f, .86f); Box(mesh, .58f, .34f, .8f, .86f); Box(mesh, .42f, .52f, .58f, .72f);
                    Tri(mesh, new Vector2(.22f, .3f), new Vector2(.4f, .3f), new Vector2(.31f, .08f));
                    Tri(mesh, new Vector2(.6f, .3f), new Vector2(.78f, .3f), new Vector2(.69f, .08f)); break;
                case Kind.Radar:
                    Ring(mesh, .5f, .5f, .42f, .34f); Ring(mesh, .5f, .5f, .24f, .16f); Disc(mesh, .5f, .5f, .07f);
                    Line(mesh, .5f, .5f, .8f, .78f, .07f); break;
                case Kind.Cross:
                    Box(mesh, .38f, .14f, .62f, .86f); Box(mesh, .14f, .38f, .86f, .62f); break;
                case Kind.Gem:
                    Tri(mesh, new Vector2(.14f, .62f), new Vector2(.86f, .62f), new Vector2(.5f, .1f));
                    Tri(mesh, new Vector2(.14f, .62f), new Vector2(.3f, .86f), new Vector2(.86f, .62f));
                    Tri(mesh, new Vector2(.3f, .86f), new Vector2(.7f, .86f), new Vector2(.86f, .62f)); break;
                case Kind.Menu:
                    Box(mesh, .18f, .22f, .82f, .32f); Box(mesh, .18f, .45f, .82f, .55f); Box(mesh, .18f, .68f, .82f, .78f); break;
                default:
                    Ring(mesh,.5f,.65f,.24f,.15f); Box(mesh,.2f,.15f,.8f,.57f); break;
            }
        }
        private Vector3 Point(Vector2 p) { var r=GetPixelAdjustedRect(); return new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height); }
        private void Tri(VertexHelper m,Vector2 a,Vector2 b,Vector2 c)
        {
            int i=m.currentVertCount; m.AddVert(Point(a),color,Vector2.zero);m.AddVert(Point(b),color,Vector2.zero);m.AddVert(Point(c),color,Vector2.zero);m.AddTriangle(i,i+1,i+2);
        }
        private void Box(VertexHelper m,float x,float y,float x1,float y1)
        { Tri(m,new Vector2(x,y),new Vector2(x,y1),new Vector2(x1,y1));Tri(m,new Vector2(x,y),new Vector2(x1,y1),new Vector2(x1,y)); }
        private void Line(VertexHelper m,float x,float y,float x1,float y1,float width)
        {
            var a=new Vector2(x,y);var b=new Vector2(x1,y1);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;
            Tri(m,a-n,a+n,b+n);Tri(m,a-n,b+n,b-n);
        }
        private void Disc(VertexHelper m,float x,float y,float r) => Ring(m,x,y,r,0);
        private void Ring(VertexHelper m,float x,float y,float outer,float inner)
        {
            var c=new Vector2(x,y);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Tri(m,c+u*outer,c+v*outer,c+v*inner);Tri(m,c+u*outer,c+v*inner,c+u*inner);
            }
        }
    }
}

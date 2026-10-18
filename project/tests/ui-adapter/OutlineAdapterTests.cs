using System;
using System.Linq;
using O=Deception.PolyModAdapter.UnitArtOutline;
namespace Deception.Tests
{
    public static class OutlineTests
    {
        static int count;
        static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
        static void Denied(Action a,string name){try{a();}catch(ArgumentException){count++;return;}catch(InvalidOperationException){count++;return;}throw new Exception(name);}
        public static void Run(Action<string,Action> test)
        {test("UI-120-outline-packed-uv-raster-and-bounded-softening",RunMath);}
        static void RunMath()
        {
            var xy=new[]{new O.V2(0,0),new O.V2(1,0),new O.V2(1,1),new O.V2(0,1)};
            var tris=new[]{0,1,2,0,2,3};var bytes=new byte[4*4*4];
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)bytes[(y*4+x)*4+3]=(byte)(y*60+x*10);
            var input=(byte[])bytes.Clone();
            var result=O.Rasterize(bytes,4,4,xy,xy,tris,new(0,0),new(1,1),0,0,1,1,8,8,2);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)Check(result[(y+2)*8+x+2]==bytes[(y*4+x)*4+3],"quad preserves bottom-left orientation");
            Check(result.Where((a,i)=>i/8<2||i/8>5||i%8<2||i%8>5).All(a=>a==0),"padding stays empty");
            Check(bytes.SequenceEqual(input),"source bytes unchanged");
            var rotated=new[]{new O.V2(0,1),new O.V2(0,0),new O.V2(1,0),new O.V2(1,1)};
            var turn=O.Rasterize(bytes,4,4,xy,rotated,tris,new(0,0),new(1,1),0,0,1,1,8,8,2);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)Check(turn[(y+2)*8+x+2]==bytes[((3-x)*4+y)*4+3],"packed rotation preserved");
            var cropUv=xy.Select(p=>new O.V2(.2f+p.X*.5f,.1f+p.Y*.6f)).ToArray();
            var crop=O.Rasterize(bytes,4,4,xy,cropUv,tris,new(0,0),new(1,1),.2f,.1f,.7f,.7f,8,8,2);
            Check(crop.SequenceEqual(result),"UV crop coordinate normalization");
            var opaque=Enumerable.Repeat((byte)255,4*4*4).ToArray();
            var triangle=O.Rasterize(opaque,4,4,xy,xy,new[]{0,1,3},new(0,0),new(1,1),0,0,1,1,8,8,2);
            Check(triangle[5*8+5]==0&&triangle[2*8+2]==255,"tight mesh excludes neighboring atlas area");
            int[] Brute(byte[] mask,int w,int h)
            {
                int absent=w*w+h*h+1;var expected=Enumerable.Repeat(absent,w*h).ToArray();
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)
                    if(mask[yy*w+xx]>=O.CoverageThreshold)
                    {int dx=x-xx,dy=y-yy;expected[y*w+x]=Math.Min(expected[y*w+x],dx*dx+dy*dy);}
                return expected;
            }
            var random=new Random(348);
            for(int trial=0;trial<100;trial++)
            {
                int w=1+random.Next(12),h=1+random.Next(12);var mask=new byte[w*h];
                for(int i=0;i<mask.Length;i++)mask[i]=(byte)(random.Next(4)==0?128+random.Next(128):random.Next(128));
                if(trial%10==0)Array.Clear(mask,0,mask.Length);
                var before=(byte[])mask.Clone();var actual=O.DistanceSquared(mask,w,h);var expected=Brute(mask,w,h);
                Check(mask.SequenceEqual(before),"EDT input preserved");
                for(int i=0;i<mask.Length;i++)Check(actual[i]==expected[i],"exact EDT matches brute force including empty/narrow axes");
            }
            var dot=new byte[65*65];dot[32*65+32]=255;var radial=O.DistanceSquared(dot,65,65);
            Check(radial[35*65+36]==25&&radial[28*65+35]==25,"exact Euclidean diagonals, not Manhattan or box distance");
            var radialHalo=O.EdgeHalo(dot,65,65);
            for(int y=0;y<65;y++)for(int x=0;x<65;x++)
                Check(radialHalo[y*65+x]==radialHalo[x*65+y]&&radialHalo[y*65+x]==radialHalo[(64-y)*65+64-x],"radial reflection and axis symmetry");
            var narrow=new byte[64*64];var wide=new byte[64*64];
            for(int y=8;y<56;y++){narrow[y*64+32]=255;for(int x=8;x<=32;x++)wide[y*64+x]=255;}
            var n=O.EdgeHalo(narrow,64,64);var b=O.EdgeHalo(wide,64,64);
            for(int d=1;d<=30;d++)Check(n[32*64+32+d]==b[32*64+32+d],"narrow and broad silhouettes have identical outward edge intensity");
            Check(n[32*64+33]==O.EdgeAlpha&&O.EdgeAlpha==180,"thin feature retains the approved 25% lower edge intensity");
            byte prior=O.EdgeAlpha;bool lowTail=false;
            for(int d=1;d<=30;d++)
            {
                byte value=n[32*64+32+d];Check(value<=prior,"outward profile monotone");prior=value;lowTail|=value>0&&value<24;
            }
            Check(lowTail&&n[32*64+32+O.BandRadius+O.TailRadius+1]==0,"soft tail ends at bounded radius");
            foreach(byte coverage in new byte[]{128,204,255})
            {
                var covered=new byte[256*256];for(int y=O.Padding;y<256-O.Padding;y++)for(int x=O.Padding;x<256-O.Padding;x++)covered[y*256+x]=coverage;
                var copy=(byte[])covered.Clone();var halo=O.EdgeHalo(covered,256,256);
                Check(covered.SequenceEqual(copy),"source alpha unchanged including204mask face");
                Check(Enumerable.Range(0,covered.Length).Where(i=>covered[i]>=O.CoverageThreshold).All(i=>halo[i]==0),"no central light beneath covered semi-transparent pixels");
                Check(halo[128*256+256-O.Padding]==O.EdgeAlpha,"coverage128/204/255alltrace strong outside edge");
                Check(Enumerable.Range(0,256).All(i=>halo[i]==0&&halo[255*256+i]==0&&halo[i*256]==0&&halo[i*256+255]==0),"full-support transparent frame border");
            }
            Check(O.EdgeHalo(new byte[16],4,4).All(v=>v==0),"empty alpha produces no light");
            Check(O.EdgeHalo(Enumerable.Repeat((byte)127,16).ToArray(),4,4).All(v=>v==0),"subcoverage specks do not seed bright halos");
            foreach(int inner in new[]{1,8,90,96,128})
            {
                int w=inner+2*O.Padding;var coverage=O.Rasterize(opaque,4,4,xy,xy,tris,new(0,0),new(1,1),0,0,1,1,w,256,O.Padding);
                var halo=O.EdgeHalo(coverage,w,256);
                Check(halo.Any(v=>v==O.EdgeAlpha),"narrow padded frame retains bright external edge");
                Check(Enumerable.Range(0,w).All(i=>halo[i]==0&&halo[255*w+i]==0)&&Enumerable.Range(0,256).All(i=>halo[i*w]==0&&halo[i*w+w-1]==0),"narrow frame transparent border");
            }
            Denied(()=>O.DistanceSquared(new byte[2],1,1),"EDT length rejected");
            Denied(()=>O.DistanceSquared(new byte[257],257,1),"EDT256cap");
            Denied(()=>O.EdgeHalo(new byte[1],0,1),"empty dimension rejected");
            Denied(()=>O.Validate(xy,xy,new[]{0,1,7},new(0,0),new(1,1)),"badtriangle rejected");
            Denied(()=>O.Validate(xy,xy,tris,new(0,0),new(float.NaN,1)),"bad bounds rejected");
            Denied(()=>O.Rasterize(bytes,4,4,xy,xy,tris,new(0,0),new(1,1),0,0,1,1,257,8,2),"raster hard cap");
            try{O.ValidateCounts(513,513,1536);throw new Exception("unbounded mesh accepted");}
            catch(InvalidOperationException e){Check(e.Message.Contains("vertex count 513")&&e.Message.Contains("512"),"vertex cap diagnostic precise");}
            try{O.ValidateCounts(4,3,6);throw new Exception("UV mismatch accepted");}
            catch(InvalidOperationException e){Check(e.Message.Contains("UV count 3")&&e.Message.Contains("vertex count 4"),"UV mismatch diagnostic precise");}
            try{O.ValidateCounts(4,4,1539);throw new Exception("index cap accepted");}
            catch(InvalidOperationException e){Check(e.Message.Contains("index count 1539")&&e.Message.Contains("1536"),"index cap diagnostic precise");}
        }
    }
}

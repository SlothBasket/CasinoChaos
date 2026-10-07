using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GWYF_CasinoChaos
{
    internal static class WorldEyeEffect
    {
        private static EyePass _pass;
        private static bool _installed,_left,_right;
        internal static void Install(){_installed=true;}
        internal static void SetState(bool left,bool right){_left=left;_right=right;}
        internal static void Queue(ScriptableRenderer renderer,Camera camera)
        {
            if(!_installed||VisionTuning.Mode!=VisionMode.VanillaExtended||(!_left&&!_right)||camera!=Camera.main)return;
            if(_pass==null)_pass=new EyePass();
            _pass.UpdateMask(_left,_right);renderer.EnqueuePass(_pass);
        }
        internal static void Shutdown(){_installed=false;_left=_right=false;_pass?.Dispose();_pass=null;}
        private sealed class EyePass:ScriptableRenderPass
        {
            private sealed class Data {internal TextureHandle World,A,B;internal EyePass Pass;internal Matrix4x4 View,Projection;internal int Width,Height;}
            private readonly Material _gaussian,_blur,_dark;
            private readonly Mesh _blurMesh,_darkMesh;
            private int _signature=int.MinValue;
            private bool _both;
            internal EyePass()
            {
                renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;requiresIntermediateTexture=true;
                // Exact installed shader/pass contract from PostProcessPassRenderGraph.BloomGaussian.
                var shader=Shader.Find("Hidden/Universal Render Pipeline/Bloom");
                if(!shader)throw new System.InvalidOperationException("Installed vanilla Bloom shader unavailable");
                _gaussian=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                _blur=new Material(Graphic.defaultGraphicMaterial){hideFlags=HideFlags.HideAndDontSave};
                _dark=new Material(Graphic.defaultGraphicMaterial){hideFlags=HideFlags.HideAndDontSave};
                _dark.mainTexture=Texture2D.whiteTexture;
                _blurMesh=Build("CasinoChaos blurred vision mask");_darkMesh=Build("CasinoChaos dark vision mask");
            }
            private static Mesh Build(string name)
            {
                const int columns=256;var vertices=new Vector3[(columns+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[columns*6];
                for(int i=0;i<=columns;i++){float x=i/(float)columns;vertices[i*2]=new Vector3(x,0,0);vertices[i*2+1]=new Vector3(x,1,0);uv[i*2]=new Vector2(x,0);uv[i*2+1]=new Vector2(x,1);}
                for(int i=0;i<columns;i++){int a=i*2,t=i*6;triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=a+2;triangles[t+3]=a+2;triangles[t+4]=a+1;triangles[t+5]=a+3;}
                var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.MarkDynamic();return mesh;
            }
            internal void UpdateMask(bool left,bool right)
            {
                _both=left&&right;
                int hash=left.GetHashCode()*31+right.GetHashCode();
                foreach(float value in new[]{VisionTuning.SingleEyeBlackStrength,VisionTuning.SingleEyeBlurStrength,VisionTuning.SingleEyeInnerReach,VisionTuning.SingleEyeFeatherWidth,VisionTuning.BothEyesBlackStrength,VisionTuning.BothEyesBlurStrength,VisionTuning.BothEyesCenterVisibility})hash=unchecked(hash*31+value.GetHashCode());
                if(hash==_signature)return;_signature=hash;
                // Single-eye masks remain vertical; both-eye masks run across
                // screen Y. Texture UVs still match real screen coordinates.
                var vertices=new Vector3[514];var uv=new Vector2[514];
                for(int i=0;i<=256;i++)
                {
                    float t=i/256f;
                    vertices[i*2]=left&&right?new Vector3(0,t,0):new Vector3(t,0,0);
                    vertices[i*2+1]=left&&right?new Vector3(1,t,0):new Vector3(t,1,0);
                    uv[i*2]=new Vector2(vertices[i*2].x,vertices[i*2].y);
                    uv[i*2+1]=new Vector2(vertices[i*2+1].x,vertices[i*2+1].y);
                }
                _darkMesh.vertices=_blurMesh.vertices=vertices;_darkMesh.uv=_blurMesh.uv=uv;
                var triangles=new int[256*6];
                for(int i=0;i<256;i++)
                {
                    int a=i*2,j=i*6;
                    triangles[j]=a;triangles[j+1]=a+1;triangles[j+2]=a+2;
                    triangles[j+3]=a+2;triangles[j+4]=a+1;triangles[j+5]=a+3;
                    if(left&&right){int swap=triangles[j];triangles[j]=triangles[j+1];triangles[j+1]=swap;swap=triangles[j+3];triangles[j+3]=triangles[j+4];triangles[j+4]=swap;}
                }
                _darkMesh.triangles=_blurMesh.triangles=triangles;
                _darkMesh.RecalculateBounds();_blurMesh.RecalculateBounds();
                var dark=new Color[514];var blur=new Color[514];
                for(int i=0;i<=256;i++){VisionTuning.Mask(i/256f,left,right,out float d,out float b);dark[i*2]=dark[i*2+1]=new Color(0,0,0,d);blur[i*2]=blur[i*2+1]=new Color(1,1,1,b);}
                _darkMesh.colors=dark;_blurMesh.colors=blur;
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
            {
                var resources=frameData.Get<UniversalResourceData>();var camera=frameData.Get<UniversalCameraData>();
                if(resources.isActiveTargetBackBuffer)return;
                var world=resources.activeColorTexture;
                var desc=world.GetDescriptor(graph);desc.name="CasinoChaos eye blur A";desc.width=Mathf.Max(1,desc.width/8);desc.height=Mathf.Max(1,desc.height/8);
                desc.msaaSamples=MSAASamples.None;desc.depthBufferBits=DepthBits.None;desc.clearBuffer=false;desc.filterMode=FilterMode.Bilinear;
                var a=graph.CreateTexture(desc);desc.name="CasinoChaos eye blur B";var b=graph.CreateTexture(desc);
                using var builder=graph.AddUnsafePass<Data>("CasinoChaos static world vision",out var data);
                data.World=world;data.A=a;data.B=b;data.Pass=this;
                data.View=camera.camera.worldToCameraMatrix;data.Projection=GL.GetGPUProjectionMatrix(camera.camera.projectionMatrix,true);
                data.Width=camera.cameraTargetDescriptor.width;data.Height=camera.cameraTargetDescriptor.height;
                builder.UseTexture(world,AccessFlags.ReadWrite);builder.UseTexture(a,AccessFlags.ReadWrite);builder.UseTexture(b,AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);builder.AllowPassCulling(false);
                builder.SetRenderFunc((Data d,UnsafeGraphContext context)=>{
                    var cmd=CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    RTHandle source=d.World,first=d.A,second=d.B;
                    Blitter.BlitCameraTexture(cmd,source,first,0,false);
                    // No bright-pixel threshold/prefilter: passes 1/2 only blur world color.
                    Blitter.BlitCameraTexture(cmd,first,second,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,d.Pass._gaussian,1);
                    Blitter.BlitCameraTexture(cmd,second,first,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,d.Pass._gaussian,2);
                    Blitter.BlitCameraTexture(cmd,first,second,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,d.Pass._gaussian,1);
                    Blitter.BlitCameraTexture(cmd,second,first,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,d.Pass._gaussian,2);
                    d.Pass._blur.mainTexture=first.rt;
                    CoreUtils.SetRenderTarget(cmd,source,RenderBufferLoadAction.Load,RenderBufferStoreAction.Store,ClearFlag.None,Color.clear);
                    cmd.SetViewport(new Rect(0,0,d.Width,d.Height));
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0,1,0,1,-1,1),true));
                    if(d.Pass._both&&VisionTuning.BothEyesBlurStrength>=1f)
                    {
                        // Full blindness already blends 100% blurred world.
                        // Use URP's own fullscreen copy, avoiding the UI mesh's
                        // render-texture orientation/projection conventions.
                        Blitter.BlitCameraTexture(cmd,first,source,0,false);
                        cmd.SetViewport(new Rect(0,0,d.Width,d.Height));
                    }
                    else cmd.DrawMesh(d.Pass._blurMesh,Matrix4x4.identity,d.Pass._blur,0,0);
                    cmd.DrawMesh(d.Pass._darkMesh,Matrix4x4.identity,d.Pass._dark,0,0);
                    cmd.SetViewProjectionMatrices(d.View,d.Projection);
                });
            }
            internal void Dispose(){Object.Destroy(_gaussian);Object.Destroy(_blur);Object.Destroy(_dark);Object.Destroy(_blurMesh);Object.Destroy(_darkMesh);}
        }
    }
    [HarmonyPatch(typeof(ScriptableRenderer),"AddRenderPasses")]
    internal static class QueueWorldEyePass
    {private static void Postfix(ScriptableRenderer __instance,ref RenderingData renderingData)=>WorldEyeEffect.Queue(__instance,renderingData.cameraData.camera);}
}

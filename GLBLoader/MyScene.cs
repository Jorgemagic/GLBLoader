using Evergine.Common.Graphics;
using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Framework.Graphics.Effects;
using Evergine.Framework.Graphics.Materials;
using Evergine.Framework.Managers;
using Evergine.Framework.Physics3D;
using Evergine.Framework.Runtimes;
using Evergine.Framework.Services;
using Evergine.Mathematics;
using GLBLoader.Micellaneous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GLBLoader
{
    public class MyScene : Scene
    {
        public override void RegisterManagers()
        {
            base.RegisterManagers();
            
            this.Managers.AddManager(new global::Evergine.Bullet.BulletPhysicManager3D());            
        }

        protected override async void CreateScene()
        {            
            List<ModelToLoad> models = new List<ModelToLoad>()
            {
                new ModelToLoad()
                {
                    Description = "Elphinestone (Polycam - Default scene)",
                    FileName = "elphinestone.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/elphinestone_fixed.glb"
                },
                new ModelToLoad()
                {
                    Description = "Elphinestone Optmised (Polycam, gltf-transform optimize draco)",
                    FileName = "elphinestone_optimised.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/elphinestone_optimised.glb"
                },
                new ModelToLoad()
                {
                    Description = "Gibraltar (Polycam - No default scene)",
                    FileName = "gibraltar.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/gibraltar.glb"
                },
                new ModelToLoad()
                {
                    Description = "Helmet (GLB Official Sample)",
                    FileName = "helmet.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/DamagedHelmet.glb"
                },
                new ModelToLoad()
                {
                    Description = "Toy Car (GLB Official Sample)",
                    FileName = "ToyCar.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/ToyCar.glb"
                },
                new ModelToLoad()
                {
                    Description = "Air Jordan (Evergine EverSneak Sample)",
                    FileName = "airjordan.glb",
                    Url = "https://redpointdemo.blob.core.windows.net/data/AirJordan.glb"
                },
            };

            // Download and load
            //var m = models.Find(m => m.FileName == "elphinestone.glb"); //Yes  (AO-DIFF-MT_RG_TEXTURED)
            var m = models.Find(m => m.FileName == "elphinestone_optimised.glb"); //Yes (AO-DIFF-MT_RG_TEXTURED)
            //var m = models.Find(m => m.FileName == "gibraltar.glb"); //Yes (DIFF)
            //var m = models.Find(m => m.FileName == "helmet.glb"); //Yes (AO-DIFF-EMIS-IBL-LIT-MT_RG_TEXTURED)
            //var m = models.Find(m => m.FileName == "ToyCar.glb"); //Yes (AO-DIFF-EMIS-IBL-LIT-MT_RG_TEXTURED, IBL-LIT, AO-DIFF-IBL-LIT)
            //var m = models.Find(m => m.FileName == "airjordan.glb"); //Yes

            var filePath = Path.Combine("Downloads", m.FileName);            
            Model model = null;
            using (HttpClient client = new HttpClient())
            {
                using (var response = await client.GetAsync(m.Url))
                {
                    response.EnsureSuccessStatusCode();

                    // Save file to disc
                    await DownloadFileTaskAsync(client, new Uri(m.Url), filePath);

                    // Read file
                    using (var fileStream = new FileStream(filePath, FileMode.Open))
                    {
                        model = await Evergine.Runtimes.GLB.GLBRuntime.Instance.Read(fileStream, this.CustomMaterialAssigner);
                    }
                }
            }

            // Instanciate model
            var assetsService = Application.Current.Container.Resolve<AssetsService>();
            var entity = model.InstantiateModelHierarchy(assetsService);
            var root = new Entity().AddComponent(new Transform3D());
            root.AddChild(entity);

            // Normalize scale
            if (model.BoundingBox.HasValue)
            {
                var boundingBox = model.BoundingBox.Value;
                boundingBox.Transform(entity.FindComponent<Transform3D>().WorldTransform);

                root.FindComponent<Transform3D>().Scale = Vector3.One * (1.0f / boundingBox.HalfExtent.Length());
                root.AddComponent(new BoxCollider3D()
                {
                    Size = boundingBox.HalfExtent * 2,
                    Offset = boundingBox.Center,
                });
                root.AddComponent(new StaticBody3D());
            }

            ((RenderManager)this.Managers.RenderManager).DebugLines = true;

            // Add to scene
            this.Managers.EntityManager.Add(root);
        }

        private static async Task DownloadFileTaskAsync(HttpClient client, Uri uri, string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var s = await client.GetStreamAsync(uri))
            {
                using (var fs = new FileStream(filePath, FileMode.CreateNew))
                {
                    await s.CopyToAsync(fs);
                }
            }
        }

        // Only Diffuse channel is needed
        private async Task<Material> CustomMaterialAssigner(MaterialData data)
        {
            var assetsService = Application.Current.Container.Resolve<AssetsService>();

            // Get textures            
            var baseColor = await data.GetBaseColorTextureAndSampler();

            // Get Layer
            var opaqueLayer = assetsService.Load<RenderLayerDescription>(DefaultResourcesIDs.OpaqueRenderLayerID);
            var alphaLayer = assetsService.Load<RenderLayerDescription>(DefaultResourcesIDs.AlphaRenderLayerID);
            RenderLayerDescription layer;
            float alpha = data.BaseColor.A / 255.0f;
            switch (data.AlphaMode)
            {
                default:
                case AlphaMode.Mask:
                case AlphaMode.Opaque:
                    layer = opaqueLayer;
                    break;
                case AlphaMode.Blend:
                    layer = alphaLayer;
                    break;
            }

            // Create standard material            
            var effect = assetsService.Load<Effect>(DefaultResourcesIDs.StandardEffectID);
            StandardMaterial standard = new StandardMaterial(effect)
            {
                BaseColor = data.BaseColor,
                Alpha = alpha,
                BaseColorTexture = baseColor.Texture,
                BaseColorSampler = baseColor.Sampler,
                Metallic = data.MetallicFactor,
                Roughness = data.RoughnessFactor,
                EmissiveColor = data.EmissiveColor.ToColor(),                
                LayerDescription = layer,
            };

            // Alpha test
            if (data.AlphaMode == AlphaMode.Mask)
            {
                standard.AlphaCutout = data.AlphaCutoff;
            }            

            return standard.Material;
        }
    }
}



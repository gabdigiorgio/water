using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Water.Cameras;
using Water.Geometries;

namespace Water
{
    public class Game1 : Game
    {
        public const string ContentFolder3D = "Models/";
        public const string ContentFolderEffects = "Effects/";
        public const string ContentFolderMusic = "Music/";
        public const string ContentFolderSounds = "Sounds/";
        public const string ContentFolderSpriteFonts = "SpriteFonts/";
        public const string ContentFolderTextures = "Textures/";
        
        private readonly GraphicsDeviceManager _graphicsDeviceManager;

        // Camera
        private FreeCamera _freeCamera;
        private readonly Vector3 _cameraInitialPosition = new(0f, 50f, 300f);
        private readonly Vector3 _lightPosition = new(500f, 500f, 300f);
        
        // Skybox
        private SkyBox _skyBox;
        private const int SkyBoxSize = 2000;
        
        // Geometries
        private TeapotPrimitive _teapot;
        private Matrix _teapotWorld;

        private TorusPrimitive _torus;
        private Matrix _torusWorld;

        private BoxPrimitive _box;
        private Matrix _boxWorld;
        
        private Effect _blinnPhongShader;
        
        // Water
        private QuadPrimitive _quad;
        private const float QuadHeight = 0f;
        private Matrix _quadWorld;
        private Effect _waterShader;
        private Texture2D _distortionMap;
        private Texture2D _normalMap;
        private const float WaveSpeed = 0.05f;
        
        // Reflection
        private RenderTarget2D _reflectionRenderTarget;
        private readonly Vector4 _reflectionClippingPlane = new(0f, 1f, 0f, -QuadHeight);
        
        // Refraction
        private RenderTarget2D _refractionRenderTarget;
        private readonly Vector4 _refractionClippingPlane = new(0f, -1f, 0f, QuadHeight);

        public Game1()
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "Water";
        }

        protected override void Initialize()
        {
            _graphicsDeviceManager.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - 100;
            _graphicsDeviceManager.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - 100;
            _graphicsDeviceManager.ApplyChanges();
            
            // Camera
            _freeCamera = new FreeCamera(GraphicsDevice.Viewport.AspectRatio, _cameraInitialPosition);
            
            // Teapot
            _teapot = new TeapotPrimitive(GraphicsDevice, 100f, 16);
            var teapotPosition = new Vector3(0f, 35f, 0f);
            _teapotWorld = Matrix.CreateRotationY(MathF.PI/2) * Matrix.CreateTranslation(teapotPosition);
            
            // Torus
            _torus = new TorusPrimitive(GraphicsDevice, 10f, 6f, 64);
            var torusScale = new Vector3(5f, 5f, 5f);
            const float torusRotationX = MathF.PI/2;
            var torusPosition = new Vector3(150f, 0f, 0f);
            _torusWorld = Matrix.CreateScale(torusScale) * Matrix.CreateRotationX(torusRotationX) * Matrix.CreateTranslation(torusPosition);
            
            // Box
            _box = new BoxPrimitive(GraphicsDevice,new Vector3(50f, 50f, 50f), null);
            var boxPosition = new Vector3(-150f, 0f, 0f);
            _boxWorld = Matrix.CreateTranslation(boxPosition);
            
            // Quad
            _quad = new QuadPrimitive(GraphicsDevice);
            var quadPosition = new Vector3(0f, QuadHeight, 0f);
            _quadWorld = Matrix.CreateScale(3000f, 0f, 3000f) * Matrix.CreateTranslation(quadPosition);
            
            _reflectionRenderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width, 
                GraphicsDevice.Viewport.Height, 
                true, SurfaceFormat.Color, DepthFormat.Depth24);
            
            _refractionRenderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width, 
                GraphicsDevice.Viewport.Height, 
                true, SurfaceFormat.Color, DepthFormat.Depth24);
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            var skyBox = Content.Load<Model>(ContentFolder3D + "skybox/cube");
            var skyBoxTexture = Content.Load<TextureCube>(ContentFolderTextures + "/skyboxes/mountain_skybox");
            var skyBoxEffect = Content.Load<Effect>(ContentFolderEffects + "SkyBox");
            _skyBox = new SkyBox(skyBox, skyBoxTexture, skyBoxEffect, SkyBoxSize);
            
            _blinnPhongShader = Content.Load<Effect>(ContentFolderEffects + "BlinnPhong");
            
            _waterShader = Content.Load<Effect>(ContentFolderEffects + "Water");
            _distortionMap = Content.Load<Texture2D>(ContentFolderTextures + "distortion_map");
            _normalMap = Content.Load<Texture2D>(ContentFolderTextures + "wave1_normal");
            
            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                Exit();
            }
            
            _freeCamera.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            DrawRefraction();
            
            DrawReflection(_quadWorld, _freeCamera.View, _freeCamera.Projection, gameTime);
            
            DrawSkyBox(_freeCamera.View, _freeCamera.Projection, _freeCamera.Position);
            
            DrawTeapot(_teapotWorld, _freeCamera.View, _freeCamera.Projection, _freeCamera.Position, Vector4.Zero);

            DrawTorus(_torusWorld, _freeCamera.View, _freeCamera.Projection, _freeCamera.Position, Vector4.Zero);
            
            DrawBox(_boxWorld, _freeCamera.View, _freeCamera.Projection, _freeCamera.Position, Vector4.Zero);

            base.Draw(gameTime);
        }
        
        private void DrawRefraction()
        {
            GraphicsDevice.SetRenderTarget(_refractionRenderTarget);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1f, 0);
            
            // Draw the scene
            DrawScene(_freeCamera.View, _freeCamera.Projection, _freeCamera.Position, _refractionClippingPlane);
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SetRenderTarget(null);
        }
        
        private void DrawReflection(Matrix world, Matrix view, Matrix projection, GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_reflectionRenderTarget);
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1f, 0);
            
            var quadNormal = Vector3.Up;
            
            var projLength = Vector3.Dot(quadNormal, _freeCamera.Position - _quadWorld.Translation);

            var reflectionCamPos = _freeCamera.Position - 2 * quadNormal * projLength;

            var reflectionCamForward = Vector3.Reflect(_freeCamera.FrontDirection, quadNormal);

            var reflectionCamUp = Vector3.Reflect(_freeCamera.UpDirection, quadNormal);
            
            var reflectionCamView = Matrix.CreateLookAt(reflectionCamPos, 
                reflectionCamPos + reflectionCamForward, reflectionCamUp);
            
            // Draw the scene
            DrawScene(reflectionCamView, projection, reflectionCamPos, _reflectionClippingPlane);
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SetRenderTarget(null);
            
            // Draw the water
            DrawWater(world, view, projection, reflectionCamView, gameTime);
        }

        private void DrawScene(Matrix view, Matrix projection, Vector3 position, Vector4 clippingPlane)
        {
            DrawSkyBox(view, projection, position);
            
            DrawTeapot(_teapotWorld, view, projection, position, clippingPlane);
            
            DrawTorus(_torusWorld, view, projection, position, clippingPlane);
            
            DrawBox(_boxWorld, view, projection, position, clippingPlane);
        }
        
                
        private void DrawWater(Matrix world, Matrix view, Matrix projection, Matrix reflectionView, GameTime gameTime)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _waterShader.CurrentTechnique = _waterShader.Techniques["Water"];

            _waterShader.Parameters["World"].SetValue(world);
            _waterShader.Parameters["WorldViewProjection"].SetValue(world * view * projection);
            _waterShader.Parameters["ReflectionView"].SetValue(reflectionView);
            _waterShader.Parameters["Projection"].SetValue(projection);
            
            _waterShader.Parameters["ReflectionTexture"]?.SetValue(_reflectionRenderTarget);
            _waterShader.Parameters["RefractionTexture"]?.SetValue(_refractionRenderTarget);
            _waterShader.Parameters["DistortionMap"].SetValue(_distortionMap);
            _waterShader.Parameters["NormalMap"]?.SetValue(_normalMap);
            _waterShader.Parameters["Tiling"].SetValue(Vector2.One * 20f);
            
            _waterShader.Parameters["MoveFactor"].SetValue(WaveSpeed * (float)gameTime.TotalGameTime.TotalSeconds);
            _waterShader.Parameters["WaveStrength"].SetValue(0.01f);
            
            _waterShader.Parameters["CameraPosition"].SetValue(_freeCamera.Position);
            _waterShader.Parameters["LightPosition"].SetValue(_lightPosition);
            _waterShader.Parameters["LightColor"].SetValue(Color.White.ToVector3());
            _waterShader.Parameters["Shininess"].SetValue(25f);
            _waterShader.Parameters["KSpecular"].SetValue(0.3f);
            
            _quad.Draw(_waterShader);
            
            GraphicsDevice.RasterizerState = previousRasterizerState;
        }
        
        private void DrawTeapot(Matrix world, Matrix view, Matrix projection, Vector3 position, Vector4 clippingPlane)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _blinnPhongShader.CurrentTechnique = _blinnPhongShader.Techniques["BasicColorDrawing"];
            
            _blinnPhongShader.Parameters["ClippingPlane"]?.SetValue(clippingPlane);
            
            _blinnPhongShader.Parameters["Color"].SetValue(Color.Green.ToVector3());
            
            _blinnPhongShader.Parameters["World"].SetValue(world);
            _blinnPhongShader.Parameters["View"].SetValue(view);
            _blinnPhongShader.Parameters["Projection"].SetValue(projection);
            _blinnPhongShader.Parameters["InverseTransposeWorld"].SetValue(Matrix.Invert(Matrix.Transpose(world)));
            
            _blinnPhongShader.Parameters["AmbientColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KAmbient"].SetValue(0.3f);
            
            _blinnPhongShader.Parameters["LightPosition"].SetValue(_lightPosition);
            
            _blinnPhongShader.Parameters["DiffuseColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KDiffuse"].SetValue(0.7f);
            
            _blinnPhongShader.Parameters["EyePosition"].SetValue(position);
            
            _blinnPhongShader.Parameters["SpecularColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KSpecular"].SetValue(1f);
            _blinnPhongShader.Parameters["Shininess"].SetValue(32f);
            
            _teapot.Draw(_blinnPhongShader);

            GraphicsDevice.RasterizerState = previousRasterizerState;
        }

        private void DrawTorus(Matrix world, Matrix view, Matrix projection, Vector3 position, Vector4 clippingPlane)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _blinnPhongShader.CurrentTechnique = _blinnPhongShader.Techniques["BasicColorDrawing"];
            
            _blinnPhongShader.Parameters["ClippingPlane"]?.SetValue(clippingPlane);
            
            _blinnPhongShader.Parameters["Color"].SetValue(Color.Blue.ToVector3());
            
            _blinnPhongShader.Parameters["World"].SetValue(world);
            _blinnPhongShader.Parameters["View"].SetValue(view);
            _blinnPhongShader.Parameters["Projection"].SetValue(projection);
            _blinnPhongShader.Parameters["InverseTransposeWorld"].SetValue(Matrix.Invert(Matrix.Transpose(world)));
            
            _blinnPhongShader.Parameters["AmbientColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KAmbient"].SetValue(0.3f);
            
            _blinnPhongShader.Parameters["LightPosition"].SetValue(_lightPosition);
            
            _blinnPhongShader.Parameters["DiffuseColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KDiffuse"].SetValue(0.7f);
            
            _blinnPhongShader.Parameters["EyePosition"].SetValue(position);
            
            _blinnPhongShader.Parameters["SpecularColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KSpecular"].SetValue(1f);
            _blinnPhongShader.Parameters["Shininess"].SetValue(32f);
            
            _torus.Draw(_blinnPhongShader);

            GraphicsDevice.RasterizerState = previousRasterizerState;
        }
        
        private void DrawBox(Matrix world, Matrix view, Matrix projection, Vector3 position, Vector4 clippingPlane)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _blinnPhongShader.CurrentTechnique = _blinnPhongShader.Techniques["BasicColorDrawing"];
            
            _blinnPhongShader.Parameters["ClippingPlane"]?.SetValue(clippingPlane);
            
            _blinnPhongShader.Parameters["Color"].SetValue(Color.Red.ToVector3());
            
            _blinnPhongShader.Parameters["World"].SetValue(world);
            _blinnPhongShader.Parameters["View"].SetValue(view);
            _blinnPhongShader.Parameters["Projection"].SetValue(projection);
            _blinnPhongShader.Parameters["InverseTransposeWorld"].SetValue(Matrix.Invert(Matrix.Transpose(world)));
            
            _blinnPhongShader.Parameters["AmbientColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KAmbient"].SetValue(0.3f);
            
            _blinnPhongShader.Parameters["LightPosition"].SetValue(_lightPosition);
            
            _blinnPhongShader.Parameters["DiffuseColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KDiffuse"].SetValue(0.7f);
            
            _blinnPhongShader.Parameters["EyePosition"].SetValue(position);
            
            _blinnPhongShader.Parameters["SpecularColor"].SetValue(Color.White.ToVector3());
            _blinnPhongShader.Parameters["KSpecular"].SetValue(1f);
            _blinnPhongShader.Parameters["Shininess"].SetValue(32f);
            
            _box.Draw(_blinnPhongShader);

            GraphicsDevice.RasterizerState = previousRasterizerState;
        }
        
        private void DrawSkyBox(Matrix view, Matrix projection, Vector3 position)
        {
            var originalRasterizerState = GraphicsDevice.RasterizerState;
            var rasterizerState = new RasterizerState();
            rasterizerState.CullMode = CullMode.None;
            GraphicsDevice.RasterizerState = rasterizerState;
            _skyBox.Draw(view, projection, position);
            GraphicsDevice.RasterizerState = originalRasterizerState;
        }
    }
}
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
        
        private readonly Vector3 _lightPosition = new(500f, 50f, 300f);
        
        // Skybox
        private SkyBox _skyBox;
        private const int SkyBoxSize = 2000;
        
        // Teapot
        private TeapotPrimitive _teapot;
        private Matrix _teapotWorld;
        private Effect _blinnPhongShader;
        
        // Water
        private QuadPrimitive _quad;
        private Matrix _quadWorld;
        private Effect _waterShader;
        private Texture2D _waveTexture;
        
        // Reflection
        private RenderTarget2D _reflectionRenderTarget;

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
            var screenSize = new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            _freeCamera = new FreeCamera(GraphicsDevice.Viewport.AspectRatio, _cameraInitialPosition, screenSize);
            
            // Teapot
            _teapot = new TeapotPrimitive(GraphicsDevice, 100f, 16);
            var teapotPosition = new Vector3(0f, 50f, 0f);
            _teapotWorld = Matrix.CreateRotationY(MathF.PI/2) * Matrix.CreateTranslation(teapotPosition);
            
            // Quad
            _quad = new QuadPrimitive(GraphicsDevice);
            _quadWorld = Matrix.CreateScale(300f, 0f, 300f) * Matrix.CreateTranslation(0f, 0f, 0f);
            
            _reflectionRenderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width, 
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
            _waveTexture = Content.Load<Texture2D>(ContentFolderTextures + "wave1_normal");
            
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

            DrawReflection(_quadWorld, _freeCamera.View, _freeCamera.Projection, gameTime);
            
            DrawSkyBox(_freeCamera.View, _freeCamera.Projection, _freeCamera.Position);
            
            DrawTeapot(_teapotWorld, _freeCamera.View, _freeCamera.Projection, _freeCamera.Position);

            base.Draw(gameTime);
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
            DrawScene(_teapotWorld, reflectionCamView, projection, reflectionCamPos);
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SetRenderTarget(null);
            
            // Draw the water
            DrawWater(world, view, projection, reflectionCamView, gameTime);
        }

        private void DrawScene(Matrix world, Matrix view, Matrix projection, Vector3 position)
        {
            DrawSkyBox(view, projection, position);
            
            DrawTeapot(world, view, projection, position);
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
            _waterShader.Parameters["NormalTexture"].SetValue(_waveTexture);
            _waterShader.Parameters["Tiling"].SetValue(Vector2.One);
            
            _waterShader.Parameters["AmbientColor"].SetValue(new Vector3(1f, 1f, 1f));
            _waterShader.Parameters["DiffuseColor"].SetValue(new Vector3(1f, 1f, 1f));
            _waterShader.Parameters["SpecularColor"].SetValue(new Vector3(1f, 1f, 1f));
            
            _waterShader.Parameters["KAmbient"].SetValue(0.8f);
            _waterShader.Parameters["KDiffuse"].SetValue(0.8f);
            _waterShader.Parameters["KSpecular"].SetValue(0.5f);
            _waterShader.Parameters["Shininess"].SetValue(32f);
            
            _waterShader.Parameters["LightPosition"].SetValue(_lightPosition);
            _waterShader.Parameters["EyePosition"].SetValue(_freeCamera.Position);
            
            _waterShader.Parameters["Time"].SetValue((float)gameTime.TotalGameTime.TotalSeconds);
            _waterShader.Parameters["ScaleTimeFactor"].SetValue(15f);
            
            _quad.Draw(_waterShader);
            
            GraphicsDevice.RasterizerState = previousRasterizerState;
        }
        
        private void DrawTeapot(Matrix world, Matrix view, Matrix projection, Vector3 position)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _blinnPhongShader.CurrentTechnique = _blinnPhongShader.Techniques["BasicColorDrawing"];
            
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
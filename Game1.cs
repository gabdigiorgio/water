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
        private Effect _blinnPhong;

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
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            var skyBox = Content.Load<Model>(ContentFolder3D + "skybox/cube");
            var skyBoxTexture = Content.Load<TextureCube>(ContentFolderTextures + "/skyboxes/mountain_skybox");
            var skyBoxEffect = Content.Load<Effect>(ContentFolderEffects + "SkyBox");
            _skyBox = new SkyBox(skyBox, skyBoxTexture, skyBoxEffect, SkyBoxSize);
            
            _blinnPhong = Content.Load<Effect>(ContentFolderEffects + "BlinnPhong");
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

            DrawSkyBox(_freeCamera.View, _freeCamera.Projection, _freeCamera.Position);
            
            DrawTeapot(_teapotWorld, _freeCamera.View, _freeCamera.Projection, _freeCamera.Position);

            base.Draw(gameTime);
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
        
        private void DrawTeapot(Matrix world, Matrix view, Matrix projection, Vector3 position)
        {
            var previousRasterizerState = GraphicsDevice.RasterizerState;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _blinnPhong.CurrentTechnique = _blinnPhong.Techniques["BasicColorDrawing"];
            
            _blinnPhong.Parameters["Color"].SetValue(Color.Green.ToVector3());
            
            _blinnPhong.Parameters["World"].SetValue(world);
            _blinnPhong.Parameters["View"].SetValue(view);
            _blinnPhong.Parameters["Projection"].SetValue(projection);
            _blinnPhong.Parameters["InverseTransposeWorld"].SetValue(Matrix.Invert(Matrix.Transpose(world)));
            
            _blinnPhong.Parameters["AmbientColor"].SetValue(Color.White.ToVector3());
            _blinnPhong.Parameters["KAmbient"].SetValue(0.3f);
            
            _blinnPhong.Parameters["LightPosition"].SetValue(_lightPosition);
            
            _blinnPhong.Parameters["DiffuseColor"].SetValue(Color.White.ToVector3());
            _blinnPhong.Parameters["KDiffuse"].SetValue(0.7f);
            
            _blinnPhong.Parameters["EyePosition"].SetValue(position);
            
            _blinnPhong.Parameters["SpecularColor"].SetValue(Color.White.ToVector3());
            _blinnPhong.Parameters["KSpecular"].SetValue(1f);
            _blinnPhong.Parameters["Shininess"].SetValue(32f);
            
            _teapot.Draw(_blinnPhong);

            GraphicsDevice.RasterizerState = previousRasterizerState;
        }
    }
}
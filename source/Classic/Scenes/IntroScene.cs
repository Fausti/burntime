﻿
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Data.BurnGfx;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.GUI;
using Burntime.Platform.IO;

namespace Burntime.Classic.Scenes
{
    struct IntroPage
    {
        public int Map;
        public float Time;
        public float Scrolling;

        public MapData MapData;
    }

    struct IntroAnimation
    {
        public Vector2 Position;
        public float Time;
        public float Start;
        public string File; 
    }

    class IntroScene : Scene
    {
        MapView view;
        FadingHelper timeout;
        IntroPage[] pages;
        int index;
        float scroll;
        float oldSpeed;
        Image image;
        IntroAnimation[] animations;
        float aniTime;
        float time;
        int activeAni;

        float delayedMusicStart;

        public IntroScene(Module App)
            : base(App)
        {
            timeout = new FadingHelper();
            Music = "20_MUS 20_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            Size = new Vector2(320, 200);
            app.RenderMouse = false;
            CaptureAllMouseClicks = true;

            ConfigFile intro = new ConfigFile();
            intro.Open("intro.txt");

            pages = new IntroPage[intro["intro"].GetInt("pages")];

            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Map = intro["page" + i].GetInt("map");
                pages[i].Time = intro["page" + i].GetFloat("time");
                pages[i].Scrolling = intro["page" + i].GetFloat("scrolling");
                DataID<MapData> mapData = app.ResourceManager.GetData("classicmap@mat_" + pages[i].Map.ToString("D3") + ".raw??" + pages[i].Map);
                pages[i].MapData = mapData.Object;
            }

            animations = new IntroAnimation[intro["intro"].GetInt("animations")];

            for (int i = 0; i < animations.Length; i++)
            {
                animations[i].Position = intro["ani" + i].GetVector2("pos");
                animations[i].Start = intro["ani" + i].GetFloat("start");
                animations[i].Time = intro["ani" + i].GetFloat("time");
                animations[i].File = intro["ani" + i].GetString("file");
            }

            view = new MapView(null, app);
            view.Enabled = false;
            view.Position = new Vector2(16, 0);
            view.Size = new Vector2(288, 160);
            Windows += view;

            image = new Image(app);
            image.Position = new Vector2(0, 0);
            image.Background = null;
            image.Layer += 20;
            Windows += image;

            PreloadAnimations();
        }

        private void PreloadAnimations()
        {
            for (int i = 0; i < animations.Length; i++)
            {
                Sprite sprite = app.ResourceManager.GetImage(animations[i].File);
                sprite.Touch();
            }
        }

        protected override void OnActivateScene(object parameter)
        {
            if (oldSpeed == 0)
                oldSpeed = app.Engine.BlendSpeed;
            app.Engine.Music.Volume = 0;
            app.Engine.BlendSpeed = 0.9f;
            app.Engine.MusicBlend = false;
            app.Engine.Music.IsMuted = true;
            delayedMusicStart = 2.0f;
            index = 0;
            scroll = 0;
            time = 0;
            activeAni = -1;
            view.Map = pages[0].MapData;
            timeout.State = 1;
            timeout.Speed = 1 / pages[0].Time;
            timeout.FadeOut();
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            NextScene();
            return true;
        }

        public override bool OnVKeyPress(Keys key)
        {
            NextScene();
            return true;
        }

        protected override void OnInactivateScene()
        {
            app.Engine.BlendSpeed = oldSpeed;
            app.RenderMouse = true;
        }

        public override void OnUpdate(float elapsed)
        {
            if (delayedMusicStart >= 0)
            {
#warning // dirty hack: mute the music for ~2 seconds to get rid of the starting sound/distortion that is part of the original music file
                delayedMusicStart -= elapsed;
                if (delayedMusicStart <= 0)
                    app.Engine.Music.IsMuted = false;
            }

            timeout.Update(elapsed);
            scroll += elapsed * pages[index].Scrolling;
            view.ScrollPosition = new Vector2((int)(-scroll + 0.5f), 0);
            view.OnUpdate(0);

            if (timeout.IsOut)
            {
                app.Engine.Blend = 1;
            }

            if (app.Engine.IsBlended)
            {
                if (index >= pages.Length - 1)
                {
                    NextScene();
                }
                else
                {
                    index++;
                    timeout.State = 1;
                    timeout.Speed = 1 / pages[index].Time;
                    timeout.FadeOut();
                    scroll = 0;
                    app.Engine.Blend = 0;

                    view.Map = pages[index].MapData;
                }
            }

            time += elapsed;
            if (aniTime > 0)
            {
                aniTime -= elapsed;
                if (aniTime <= 0)
                {
                    image.Background = null;
                }
            }

            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].Start >= time && animations[i].Start < time + 0.5f)
                {
                    if (activeAni != i)
                    {
                        activeAni = i;
                        image.Background = animations[i].File;
                        image.Background.Animation.Progressive = false;
                        image.Position = animations[i].Position;
                        aniTime = animations[i].Time - (time - animations[i].Start);
                    }
                }
            }
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            target.Layer+=50;

            target.RenderRect(new Vector2(-48, 0), new Vector2(64, 160), new PixelColor(0, 0, 0));
            target.RenderRect(new Vector2(304, 0), new Vector2(64, 160), new PixelColor(0, 0, 0));
            target.RenderRect(new Vector2(0, 160), new Vector2(320, 40), new PixelColor(0, 0, 0));
        }

        private void NextScene()
        {
            app.Engine.MusicBlend = true;
            app.SceneManager.SetScene("MenuScene");
        }
    }
}

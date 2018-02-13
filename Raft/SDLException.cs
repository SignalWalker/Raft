namespace VulkanCubes {
    using System;
    using SDL2;

    public class SDLException : Exception {

        public SDLException() : base(SDL.SDL_GetError()) { }

    }
}
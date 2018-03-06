namespace Raft.Input {
    using System.Collections.Generic;
    using SDL2;

    public class InputStream {

        public static Queue<SDL.SDL_Event> ReadAll() {
            Queue<SDL.SDL_Event> res = new Queue<SDL.SDL_Event>();
            while (SDL.SDL_PollEvent(out SDL.SDL_Event inp) == 1) {
                res.Enqueue(inp);
            }
            return res;
        }

    }
}
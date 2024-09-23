
using System;
using System.Diagnostics;

namespace mcutils
{
    public class Clock
    {
        private Stopwatch sw;
        private long last;

        public static long Micros(long ticks)
        {
            return (long)((1e6*ticks) / Stopwatch.Frequency);
        }

        public static long Millis(long ticks)
        {
            return (long)((1e3*ticks) / Stopwatch.Frequency);
        }

        public Clock()
        {
            this.sw = new Stopwatch ();
        }

        public void Reset()
        {
            sw.Reset();
            this.last = Now();
        }

        public void Start()
        {
            sw.Start();
        }

        public void Stop()
        {
            sw.Stop();
        }

        public void Restart()
        {
            sw.Stop();
            sw.Reset();
            this.last = Now();
            sw.Start();
        }

        public long Now()
        {
            return sw.ElapsedTicks;
        }

        public void Mark()
        {
            this.last = Now();
        }

        public long Last()
        {
            return this.last;
        }

        public long Delta()
        {
            long now = Now();

            long value = now - this.last;
            this.last = now;

            return value;
        }
    }
}

using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;
using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;

namespace TurbineController
{
    public class Program
    {
        private static DateTime sample;
        private static DateTime lastSample;
        private static long interval;
        private static long lastInterval;
        private static bool pulseCodeComplete;

        public static void Main()
        {
            Debug.Print(
                Resources.GetString(Resources.StringResources.String1));

            var collector = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di0, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);            
            collector.OnInterrupt += collector_OnInterrupt;

            while (true)
            {
                // We can do other work here, like updating
                // an LCD display or something.
                Thread.Sleep(3000);
            }
        }

        static void collector_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            if (data2 == 0) // Edge going low, start of pulse.
            {
                sample = time;
            }
            else            // Edge going high, end of pulse.
            {
                lastSample = sample;
                sample = time;
                pulseCodeComplete = !pulseCodeComplete;

                if (!pulseCodeComplete)
                {
                    interval = (sample - lastSample).Ticks / 10000;
                }
                else
                {
                    lastInterval = interval;
                    interval = (sample - lastSample).Ticks / 10000;

                    Debug.Print(lastInterval + "/" + interval + ", rotation direction: " + (interval > lastInterval ? "+" : "-"));

                    // Reset timers.
                    lastInterval = 0;
                    interval = 0;
                    sample = DateTime.MinValue;
                    lastSample = DateTime.MinValue;
                }
            }
        }
    }
}

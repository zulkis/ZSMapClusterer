using System;
using System.Windows.Threading;

namespace ZSMapClusterer.Extensions
{
    public static class ActionExtensions
    {
        public static DispatcherTimer RunAfter(this Action action, TimeSpan span)
        {
            var dispatcherTimer = new DispatcherTimer { Interval = span };
            dispatcherTimer.Tick += (sender, args) =>
            {
                var timer = sender as DispatcherTimer;
                if (timer != null)
                {
                    timer.Stop();
                }

                action();
            };
            dispatcherTimer.Start();
            return dispatcherTimer;
        }
    }

    public static class ActionUtil
    {
        public static DispatcherTimer Run(Action action, TimeSpan afterSpan)
        {
           return action.RunAfter(afterSpan);
        }
    }
}

using Aimmy2.Class;
using InputLogic;
using System.Windows.Threading;

namespace Aimmy2.AILogic
{
    public class AntiRecoilManager
    {
        public DispatcherTimer HoldDownTimer = new();
        public int IndependentMousePress = 0;

        public void HoldDownLoad()
        {
            if (HoldDownTimer != null)
            {
                HoldDownTimer.Tick += new EventHandler(HoldDownTimerTicker!);
                HoldDownTimer.Interval = TimeSpan.FromMilliseconds(1);
            }
        }

        private void HoldDownTimerTicker(object sender, EventArgs e)
        {
            IndependentMousePress += 1;
            if (IndependentMousePress >= Convert.ToInt32(Dictionary.sliderSettings["AR Hold Time"]))
                MouseManager.DoAntiRecoil();
        }
    }
}
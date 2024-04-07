using AeroWizard;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DriverUpdater
{
    public class WizardUx : ProgressInterface
    {
        private WizardControl WizardControl;
        private WizardPage WizardPage;
        private bool Exit = false;

        private ProgressBar WizardProgressBar;
        private Label WizardProgressMessage;
        private SynchronizationContext synchronizationContext;
        private CloseLessForm WizardForm;
        private Label WizardDescription;

        public void Close()
        {
            Exit = true;
            synchronizationContext.Post(_ =>
            {
                WizardForm.Close();
                WizardForm.Dispose();
                WizardForm = null;
            }, null);
        }

        public class CloseLessForm : Form
        {
            private const int CP_NOCLOSE_BUTTON = 0x200;
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams myCp = base.CreateParams;
                    myCp.ClassStyle |= CP_NOCLOSE_BUTTON;
                    return myCp;
                }
            }
        }

        public class GrowLabel : Label
        {
            private bool mGrowing;
            public GrowLabel()
            {
                AutoSize = false;
            }
            private void resizeLabel()
            {
                if (mGrowing)
                {
                    return;
                }

                try
                {
                    mGrowing = true;
                    Size sz = new(Width, int.MaxValue);
                    sz = TextRenderer.MeasureText(Text, Font, sz, TextFormatFlags.WordBreak);
                    Height = sz.Height;
                }
                finally
                {
                    mGrowing = false;
                }
            }
            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                resizeLabel();
            }
            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                resizeLabel();
            }
            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                resizeLabel();
            }
        }

        public void Initialize()
        {
            Application.EnableVisualStyles();
            _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            //
            // MainForm
            //
            WizardForm = new CloseLessForm
            {
                MinimumSize = new Size(601, 441),
                ShowIcon = false,
                Text = "Driver Updater",
                AutoScaleDimensions = new SizeF(7F, 15F),
                AutoScaleMode = AutoScaleMode.Font,
                ClientSize = new Size(670, 479),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Margin = new Padding(4, 3, 4, 3),
                MaximizeBox = false,
                MinimizeBox = false,
                Name = "WizardUx",
                StartPosition = FormStartPosition.CenterScreen
            };

            int scalingFactor = WizardForm.DeviceDpi / 96;

            //
            // WizardControl
            //
            WizardControl = new WizardControl
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
                Location = new Point(0, 0),
                Margin = new Padding(4 * scalingFactor, 3 * scalingFactor, 4 * scalingFactor, 3 * scalingFactor),
                Name = "WizardControl",
                Size = new Size(670 * scalingFactor, 479 * scalingFactor),
                TabIndex = 0,
                Title = "Driver Updater",
                TitleIcon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName)
            };

            WizardForm.Controls.Add(WizardControl);

            //
            // WizardPage
            //
            WizardPage = new WizardPage
            {
                AllowBack = false,
                AllowCancel = false,
                AllowNext = false,
                Dock = DockStyle.Fill,
                IsFinishPage = true,
                Location = new Point(0, 0),
                Margin = new Padding(0),
                Name = "WizardPage",
                ShowCancel = false,
                ShowNext = false,
                Size = new Size(623 * scalingFactor, 325 * scalingFactor),
                TabIndex = 0,
                Text = "Preparing"
            };

            WizardControl.Pages.Add(WizardPage);

            WizardDescription = new GrowLabel
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 0),
                Name = "label2",
                Text = "Driver Updater is currently updating your Windows Installation on your device. Please wait while your installation gets serviced. This may take a while."
            };

            WizardPage.Controls.Add(WizardDescription);

            Panel panel = new()
            {
                AutoSize = true,
                MinimumSize = new Size(0, 0),
                Dock = DockStyle.Fill
            };

            //
            // label1
            //
            WizardProgressMessage = new GrowLabel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F),
                Name = "label1",
                Text = "Doing something"
            };

            panel.Controls.Add(WizardProgressMessage);

            //
            // progressBar1
            //
            WizardProgressBar = new ProgressBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 28 * scalingFactor),
                Name = "progressBar1",
                MinimumSize = new Size(0, 24 * scalingFactor)
            };

            panel.Controls.Add(WizardProgressBar);

            WizardPage.Controls.Add(panel);

            synchronizationContext = SynchronizationContext.Current;
        }

        public void ReportProgress(int? ProgressPercentage, string StatusTitle, string StatusMessage)
        {
            synchronizationContext?.Post(_ =>
            {
                if (!string.IsNullOrEmpty(StatusTitle))
                {
                    WizardPage.Text = StatusTitle;
                }

                if (!string.IsNullOrEmpty(StatusMessage))
                {
                    WizardProgressMessage.Text = StatusMessage;
                }

                if (ProgressPercentage.HasValue)
                {
                    WizardProgressBar.MarqueeAnimationSpeed = 0;
                    WizardProgressBar.Value = ProgressPercentage.Value;
                    WizardProgressBar.Style = ProgressBarStyle.Continuous;
                }
                else
                {
                    WizardProgressBar.MarqueeAnimationSpeed = 100;
                    WizardProgressBar.Style = ProgressBarStyle.Marquee;
                }
            }, null);
        }

        public void Show()
        {
            WizardForm.Show();

            while (!Exit)
            {
                Application.DoEvents();

                if (WizardForm == null || WizardForm.IsDisposed)
                {
                    Exit = true;
                }
            }
        }
    }
}
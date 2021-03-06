using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Media;
using Microsoft.DirectX.DirectSound;
using System.Threading;
using System.Xml.Serialization;
using System.IO;

namespace SN76477
{
    public partial class EmulatorForm : Form
    {

        #region Locals

        private SoundPlayer _wavPlayer;
        private SN76477 _sn;
        private uint _samplingRate = 96000;
        private bool _bufferPlaying = false;
        private Device _directSoundDevice;
        private Thread _playbackThread;
        private string _currentFilename = "<new>";
        private const string _xml_file_standard = "SN76477.xml";
        private const string _wav_file_standard = "SN76477.wav";

        #endregion


        #region Form

        void DisplayApplicationTitle()
        {
            this.Text = "SN76477 Emulator - " + _currentFilename;
        }

        public EmulatorForm()
        {
            InitializeComponent();
            _wavPlayer = new SoundPlayer();
            DisplayApplicationTitle();
        }

        private void StartPlay()
        {
            _bufferPlaying = true;
            _playbackThread = new Thread(new ParameterizedThreadStart(PlayThread));
            _playbackThread.Name = "Playback";
            _playbackThread.Start(this);
        }

        private void StopPlay()
        {
            _bufferPlaying = false;
        }

        private static void PlayThread(object osn)
        {
            EmulatorForm myform = (EmulatorForm)osn;

            SecondaryBuffer SecBuf;
            AutoResetEvent SecBufNotifyAtHalf = new AutoResetEvent(false);
            AutoResetEvent SecBufNotifyAtBeginning = new AutoResetEvent(false);
            
            int SamplingRate = (int)myform._samplingRate;
            int HoldThisManySamples = (int)(1 * SamplingRate);
            int BlockAlign = 2;
            int SecBufByteSize = HoldThisManySamples * BlockAlign;

            WaveFormat MyWaveFormat = new WaveFormat();

            // Set the format
            MyWaveFormat.AverageBytesPerSecond = (int)(myform._samplingRate * BlockAlign);
            MyWaveFormat.BitsPerSample = (short)16;
            MyWaveFormat.BlockAlign = (short)BlockAlign;
            MyWaveFormat.Channels = (short)1;
            MyWaveFormat.SamplesPerSecond = (int)myform._samplingRate;
            MyWaveFormat.FormatTag = WaveFormatTag.Pcm;

            BufferDescription MyDescription;

            // Set BufferDescription
            MyDescription = new BufferDescription();

            MyDescription.Format = MyWaveFormat;
            MyDescription.BufferBytes = HoldThisManySamples * BlockAlign;
            MyDescription.CanGetCurrentPosition = true;
            MyDescription.ControlPositionNotify = true;
            MyDescription.GlobalFocus = true;

            // Create the buffer
            SecBuf = new SecondaryBuffer(MyDescription,myform._directSoundDevice);

            Notify MyNotify;

            MyNotify = new Notify(SecBuf);

            BufferPositionNotify[] MyBufferPositions = new BufferPositionNotify[2];

            MyBufferPositions[0].Offset = 0;
            MyBufferPositions[0].EventNotifyHandle = SecBufNotifyAtBeginning.Handle;
            MyBufferPositions[1].Offset = (HoldThisManySamples / 2) * BlockAlign;
            MyBufferPositions[1].EventNotifyHandle = SecBufNotifyAtHalf.Handle;

            MyNotify.SetNotificationPositions(MyBufferPositions);

            WaitHandle[] SecBufWaitHandles = { SecBufNotifyAtBeginning, SecBufNotifyAtHalf };
            
            Int16[] buffer;

            buffer = myform._sn.GenerateSamples((uint)HoldThisManySamples, "");
            SecBuf.Write(0, buffer, LockFlag.None);
            SecBuf.Play(0, BufferPlayFlags.Looping);
            
            int SecBufNextWritePosition = 0;

            while (myform._bufferPlaying)
            {
                int WriteCount = 0,
                    PlayPosition = SecBuf.PlayPosition,
                    WritePosition = SecBuf.WritePosition;

                if (SecBufNextWritePosition < PlayPosition
                    && (WritePosition >= PlayPosition || WritePosition < SecBufNextWritePosition))
                    WriteCount = PlayPosition - SecBufNextWritePosition;
                else if (SecBufNextWritePosition > WritePosition
                    && WritePosition >= PlayPosition)
                    WriteCount = (SecBufByteSize - SecBufNextWritePosition) + PlayPosition;
               // System.Diagnostics.Debug.WriteLine("WC: "+WriteCount.ToString());
                if (WriteCount > 0)
                {
                    WriteCount = (int)Math.Min(WriteCount,1000);

                    buffer = myform._sn.GenerateSamples((uint)WriteCount/2, "");
                    
                    SecBuf.Write(
                        SecBufNextWritePosition,
                        buffer,
                        LockFlag.None);

                    SecBufNextWritePosition = (SecBufNextWritePosition + WriteCount) % SecBufByteSize;
                }
                else
                {
                    WaitHandle.WaitAny(SecBufWaitHandles, new TimeSpan(0, 0, 5), true);
                }
            }

            SecBuf.Dispose();
            MyDescription.Dispose();
            MyNotify.Dispose();

        }

        private void EmulatorForm_Load(object sender, EventArgs e)
        {
            _directSoundDevice = new Device();
            _directSoundDevice.SetCooperativeLevel(this, CooperativeLevel.Priority);

            SetWiderstandsreihe(12,  1000, 10000000, this.cbSLFRES);
            SetWiderstandsreihe(12,  1000, 10000000, this.cbVCORES);
            SetWiderstandsreihe(12,  2700,  1000000, this.cbNOISEFILTERRES);
            SetWiderstandsreihe(12, 10000,  3300000, this.cbNOISEGENRES);
            SetWiderstandsreihe(12,  2700, 10000000, this.cbNOISEFILTERRES);
            SetWiderstandsreihe(12, 10000, 10000000, this.cbONESHOTRES);
            SetWiderstandsreihe(12,  1000, 10000000, this.cbENVATKRES);
            SetWiderstandsreihe(12,  1000, 10000000, this.cbENVDECRES);
            SetWiderstandsreihe(12,  1000, 10000000, this.cbFEEDBACKRES);
            SetWiderstandsreihe(12,  1000, 10000000, this.cbAMPLITUDERES);

            SetKondensatorreihe(0, 100000, this.cbENVCAP);
            SetKondensatorreihe(0, 100000, this.cbNOISEFILTERCAP);
            SetKondensatorreihe(0, 100000, this.cbONESHOTCAP);
            SetKondensatorreihe(0, 100000, this.cbSLFCAP);
            SetKondensatorreihe(0, 100000, this.cbVCOCAP);

            _sn = this.LoadFile(_xml_file_standard);
            if (_sn == null) _sn = new SN76477();

            DisplayConfiguration(_sn);
        }

        private void EmulatorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_playbackThread != null)
            {
                _bufferPlaying = false;
                _playbackThread.Join(120);
                if (_playbackThread.IsAlive)
                {
                    System.Diagnostics.Debug.WriteLine("Trying ugly abort of playback thread, wait...");
                    _playbackThread.Abort();
                    while (_playbackThread.IsAlive) ;
                    System.Diagnostics.Debug.WriteLine("...victory is mine!");
                }
            }
        }


        #endregion

        #region UserValueChanges

        private void trackBarVCORES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.VCORESVAR = trackBarVCORES.Value;
                DisplayVCOData(_sn);
            }
        }

        private void trackBarVLFResistor_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.SLFRES = trackBarSLFRES.Value;
                DisplaySLFData(_sn);
            }
        }

        private void cbSLFCAP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.SLFCAP = GetCapacitanceFromCombobox(cbSLFCAP);
                DisplaySLFData(_sn);
            }
        }

        private void trackBarSLFRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.SLFRESVAR = trackBarSLFRES.Value;
                DisplaySLFData(_sn);
            }
        }

        private void rbENVVCO_CheckedChanged(object sender, EventArgs e)
        {
            CheckEnvelopeSetting();
        }

        private void rbENVVCOALT_CheckedChanged(object sender, EventArgs e)
        {
            CheckEnvelopeSetting();
        }

        private void rbENVMIXER_CheckedChanged(object sender, EventArgs e)
        {
            CheckEnvelopeSetting();
        }

        private void rbENVONESHOT_CheckedChanged(object sender, EventArgs e)
        {
            CheckEnvelopeSetting();
        }

        private void checkBoxMIXSLF_KeyPress(object sender, KeyPressEventArgs e)
        {
            checkBoxMIXSLF.Checked = !checkBoxMIXSLF.Checked;
            CheckMixerSetting();
        }

        private void checkBoxMIXSLF_MouseUp(object sender, MouseEventArgs e)
        {
            checkBoxMIXSLF.Checked = !checkBoxMIXSLF.Checked;
            CheckMixerSetting();
        }

        private void checkBoxMIXVCO_KeyPress(object sender, KeyPressEventArgs e)
        {
            checkBoxMIXVCO.Checked = !checkBoxMIXVCO.Checked;
            CheckMixerSetting();
        }

        private void checkBoxMIXVCO_MouseUp(object sender, MouseEventArgs e)
        {
            checkBoxMIXVCO.Checked = !checkBoxMIXVCO.Checked; 
            CheckMixerSetting();
        }

        private void checkBoxMIXNOISE_KeyPress(object sender, KeyPressEventArgs e)
        {
            checkBoxMIXNOISE.Checked = !checkBoxMIXNOISE.Checked;
            CheckMixerSetting();
        }

        private void checkBoxMIXNOISE_MouseUp(object sender, MouseEventArgs e)
        {
            checkBoxMIXNOISE.Checked = !checkBoxMIXNOISE.Checked;
            CheckMixerSetting();
        }

        private void cbVCOCAP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.VCOCAP = GetCapacitanceFromCombobox(cbVCOCAP);
                DisplayVCOData(_sn);
            }
        }

        private void cbVCORES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.VCORES = GetResistanceFromCombobox(cbVCORES);
                DisplayVCOData(_sn);
            }
        }

        private void cbNOISEGENRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.NOISECLOCKRES = GetResistanceFromCombobox(cbNOISEGENRES);
                DisplayNoiseData(_sn);
            }
        }

        private void cbNOISEFILTERCAP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.NOISEFILTERCAP = GetCapacitanceFromCombobox(cbNOISEFILTERCAP);
                DisplayNoiseData(_sn);
            }
        }

        private void cbNOISEFILTERRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.NOISEFILTERRES = GetResistanceFromCombobox(cbNOISEFILTERRES);
                DisplayNoiseData(_sn);
            }
        }

        private void cbONESHOTCAP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ONESHOTCAP = GetCapacitanceFromCombobox(cbONESHOTCAP);
                DisplayOneShotData(_sn);
            }
        }

        private void cbONESHOTRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ONESHOTRES = GetResistanceFromCombobox(cbONESHOTRES);
                DisplayOneShotData(_sn);
            }
        }

        private void cbENVCAP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ENVCAP = GetCapacitanceFromCombobox(cbENVCAP);
                DisplayEnvelopeData(_sn);
            }
        }

        private void cbENVATKRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ENVATKRES = GetResistanceFromCombobox(cbENVATKRES);
                DisplayEnvelopeData(_sn);
            }
        }   

        private void cbENVDECRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ENVDECRES = GetResistanceFromCombobox(cbENVDECRES);
                DisplayEnvelopeData(_sn);
            }
        }

        private void btnONESHOTSHOOT_Click(object sender, EventArgs e)
        {
            _sn.INHIBIT = 0;
        }

        private void btnONESHOTSHOOT_KeyDown(object sender, KeyEventArgs e)
        {
            _sn.INHIBIT = 1;
        }

        private void btnONESHOTSHOOT_MouseDown(object sender, MouseEventArgs e)
        {
            _sn.INHIBIT = 1;
        }

        private void checkBoxVCOEXT_CheckedChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                if (checkBoxVCOEXT.Checked) _sn.VCOSELECT = 0;
                else _sn.VCOSELECT = 1;
                DisplayVCOData(_sn);
            }
        }

        private void cbSLFRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.SLFRES = GetResistanceFromCombobox(cbSLFRES);
                DisplaySLFData(_sn);
            }
        }

        private void trackBarVCOEXT_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.VCOEXTVOLTAGE = (double)trackBarVCOEXT.Value / 100.0;
                DisplayVCOData(_sn);
            }
        }

        private void trackBarVCOPITCH_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.VCOPITCHVOLTAGE = (double)trackBarVCOPITCH.Value / 100.0;
                DisplayVCOData(_sn);
            }
        }

        #endregion



        #region General

        private double GetResistanceFromCombobox(ComboBox box)
        {
            try
            {
                if (box.SelectedItem.ToString() == "Not connected") return 0;

                double newvalue = System.Convert.ToDouble(box.SelectedItem.ToString());
                return newvalue;
            }
            catch
            {
                return 0;
            }
        }

        private void SetResistanceToCombobox(double value, ComboBox box)
        {
            string text;
            
            if (value == 0) text = "Not connected";
            else text = value.ToString();

            foreach (Object obj in box.Items)
            {
                if (((String)obj) == text) box.SelectedItem = obj;
            }
        }

        private double[] Widerstandsreihe(uint n)
        {
            double[] E24 = { 1, 1.1, 1.2, 1.3, 1.5, 1.6, 1.8, 2, 2.2, 2.4, 2.7, 3, 3.3, 3.6, 3.9, 4.3, 4.7, 5.1, 5.6, 6.2, 6.8, 7.5, 8.2, 9.1 };

            return E24;

            /*
             // So sollte es gehen  kommen aber nicht die real kaufbaren Reihen raus...
             * 
            double k = Math.Round(Math.Pow(10, (double)1.0 / (double)n),4);
            
            double value = 1.0;

            double[] rows = new double[n];
            
            int count = 0;

            while (count < n)
            {
                System.Diagnostics.Debug.WriteLine("cnt "+ count.ToString() + " " + value + " " +Math.Ceiling(value*10)/10);

                if (n <= 24) rows[count] = Math.Ceiling(value*10)/10;
                else rows[count] = Math.Ceiling(value*100)/100;

                value = value * k;
                count++;
            }

            return rows;
             * */

        }

        private void SetWiderstandsreihe(uint n, double min, double max, ComboBox box)
        {
            box.Items.Clear();
            double[] rows = Widerstandsreihe(n);

            int i;
            int mult = 100;
            box.Items.Add("Not connected");

            for (i = 1; i < 6; i++)
            {

                foreach (double d in rows)
                {
                    double value = d * mult;
                    if ((value >= min) && (value <= max)) box.Items.Add(value.ToString());
                }

                mult = mult * 10;
            }
            box.SelectedIndex = 0;
        }


        private double GetCapacitanceFromCombobox(ComboBox box)
        {
            try
            {
                if (box.SelectedItem.ToString() == "Not connected") return 0;

                double newvalue = System.Convert.ToDouble(box.SelectedItem.ToString());
                return newvalue / 1000000000;
            }
            catch
            {
                return 0;
            }
        }

        private void SetCapacitanceToCombobox(double value, ComboBox box)
        {
            string text;
            
            if (value == 0) text = "Not connected";
            else text = (value * 1000000000.0).ToString();

            foreach (Object obj in box.Items)
            {
                if (((String)obj) == text) box.SelectedItem = obj;
            }
        }

        private void SetKondensatorreihe(double min, double max, ComboBox box)
        {
            box.Items.Clear();
            box.Items.Add("Not connected");
            box.Items.Add("0,015");
            box.Items.Add("0,100");
            box.Items.Add("0,220");
            box.Items.Add("0,350");
            box.Items.Add("0,380");
            box.Items.Add("0,390");
            box.Items.Add("0,500");
            box.Items.Add("0,680");
            box.Items.Add("1,000");
            box.Items.Add("2,200");
            box.Items.Add("10");
            box.Items.Add("22");
            box.Items.Add("47");
            box.Items.Add("100");
            box.Items.Add("220");
            box.Items.Add("380");
            box.Items.Add("470");
            box.Items.Add("680");
            box.Items.Add("1000");
            box.Items.Add("2200");
            box.Items.Add("4700");
            box.Items.Add("10000");
            box.Items.Add("22000");
            box.Items.Add("47000");
            box.SelectedIndex = 0;
        }


        private void CheckMixerSetting()
        {
            if ((checkBoxMIXVCO.Checked) && (!checkBoxMIXSLF.Checked) && (!checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.VCO;
            else if ((!checkBoxMIXVCO.Checked) && (checkBoxMIXSLF.Checked) && (!checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.SLF;
            else if ((!checkBoxMIXVCO.Checked) && (!checkBoxMIXSLF.Checked) && (checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.NOISE;
            else if ((checkBoxMIXVCO.Checked) && (!checkBoxMIXSLF.Checked) && (checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.VCO_NOISE;
            else if ((!checkBoxMIXVCO.Checked) && (checkBoxMIXSLF.Checked) && (checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.SLF_NOISE;
            else if ((checkBoxMIXVCO.Checked) && (checkBoxMIXSLF.Checked) && (checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.SLF_VCO_NOISE;
            else if ((checkBoxMIXVCO.Checked) && (checkBoxMIXSLF.Checked) && (!checkBoxMIXNOISE.Checked)) _sn.MixerMode = MixerMode.SLF_VCO;
            else _sn.MixerMode = MixerMode.INHIBIT;
        }


        private void CheckEnvelopeSetting()
        {
            if (rbENVVCO.Checked) _sn.EnvelopeMode = EnvelopeMode.VCO;
            else if (rbENVMIXER.Checked) _sn.EnvelopeMode = EnvelopeMode.MIXER;
            else if (rbENVONESHOT.Checked) _sn.EnvelopeMode = EnvelopeMode.ONESHOT;
            else if (rbENVVCOALT.Checked) _sn.EnvelopeMode = EnvelopeMode.VCO_ALTERNATING;
        }

        private void SetVoltageToTrackBar(double value, TrackBar tb)
        {
            tb.Value = (int)Math.Round(value*100);
        }

        private void DisplayOneShotData(SN76477 sn)
        {
            labelOneShotLength.Text = (Math.Round(sn.OneShotTime, 2)).ToString() + " sec";
            labelONESHOTRESTOTAL.Text = (Math.Round((sn.ONESHOTRES + sn.ONESHOTRESVAR) / 1000, 0)).ToString() + " kΩ";
        }

        private void DisplaySLFData(SN76477 sn)
        {
            labelSLFFrequency.Text = (Math.Round(sn.SLF_Frequency,2)).ToString() + " Hz";
            labelSLFRESTOTAL.Text = (Math.Round((sn.SLFRES+sn.SLFRESVAR)/1000,0)).ToString() + " kΩ";
        }

        private void DisplayEnvelopeData(SN76477 sn)
        {
            labelAttackLength.Text = (Math.Round(sn.Attack_Time, 2)).ToString() + " sec";
            labelDecayLength.Text = (Math.Round(sn.Decay_Time, 2)).ToString() + " sec";
            labelATKRESTOTAL.Text = (Math.Round((sn.ENVATKRES + sn.ENVATKRESVAR) / 1000, 0)).ToString() + " kΩ";
            labelDECRESTOTAL.Text = (Math.Round((sn.ENVDECRES + sn.ENVDECRESVAR) / 1000, 0)).ToString() + " kΩ";
        }

        private void DisplayNoiseData(SN76477 sn)
        {
            labelNoiseFrequency.Text = (Math.Round(sn.NoiseGenerator_Frequency, 2)).ToString() + " Hz";
            labelNoiseFilterFrequency.Text  = (Math.Round(sn.NoiseFilter_Frequency, 2)).ToString() + " Hz";
            labelNOISEGENRESTOTAL.Text = (Math.Round((sn.NOISECLOCKRES + sn.NOISECLOCKRESVAR) / 1000, 0)).ToString() + " kΩ";
            labelNOISEFILTERRESTOTAL.Text = (Math.Round((sn.NOISEFILTERRES + sn.NOISEFILTERRESVAR) / 1000, 0)).ToString() + " kΩ";
        }

        private void DisplayOutputData(SN76477 sn)
        {
            labelOutputVoltageMinMax.Text = String.Format("{0}V - {1}V (clip {2}V)",
                Math.Round(sn.OUTVOLTAGEMIN,2), Math.Round(sn.OUTVOLTAGEMAX,2),3.51);
        }


        private void DisplayVCOData(SN76477 sn)
        {
            if (_sn.VCO_FrequencyMin < 1) labelVCOFrequency.Text = (Math.Round(1 / _sn.VCO_FrequencyMin, 2)).ToString() + " sec";
            else labelVCOFrequency.Text = (Math.Round(_sn.VCO_FrequencyMin,2)).ToString() + " Hz";
            labelVCORESTOTAL.Text = (Math.Round((sn.VCORES + sn.VCORESVAR) / 1000, 0)).ToString() + " kΩ";
            labelVCOEXTVoltage.Text = (Math.Round(sn.VCOEXTVOLTAGE,2)).ToString() + " V";
            labelVCOEXTFrequency.Text = (Math.Round(sn.VCO_ExternalVoltage_Frequency, 2)).ToString() + " Hz";
            labelVCOPitchVoltage.Text = (Math.Round(sn.VCOPITCHVOLTAGE,2)).ToString() + " V";
            labelVCOPitchDutyCycle.Text = (Math.Round(sn.VCO_DutyCycle, 2)).ToString() + " %";
        }

        private void DisplayConfiguration(SN76477 sn)
        {
            SetResistanceToCombobox(sn.SLFRES, cbSLFRES);
            trackBarSLFRES.Minimum = 0;
            trackBarSLFRES.Maximum = (int)sn.SLFRESVARMAX;
            trackBarSLFRES.Value = (int)sn.SLFRESVAR;
            SetCapacitanceToCombobox(sn.SLFCAP, cbSLFCAP);
            DisplaySLFData(sn);
            
            SetResistanceToCombobox(sn.VCORES, cbVCORES);
            trackBarVCORES.Minimum = 0;
            trackBarVCORES.Maximum = (int)sn.VCORESVARMAX;
            trackBarVCORES.Value = (int)sn.VCORESVAR;

            SetCapacitanceToCombobox(sn.VCOCAP, cbVCOCAP);
            SetVoltageToTrackBar(sn.VCOEXTVOLTAGE, trackBarVCOEXT);
            SetVoltageToTrackBar(sn.VCOPITCHVOLTAGE, trackBarVCOPITCH);
            if (sn.VCOSELECT == 0) checkBoxVCOEXT.Checked = true;
            else checkBoxVCOEXT.Checked = false; 
            DisplayVCOData(sn);
            
            SetResistanceToCombobox(sn.NOISECLOCKRES, cbNOISEGENRES);
            trackBarNOISEGENRES.Minimum = 0;
            trackBarNOISEGENRES.Maximum = (int)sn.NOISECLOCKRESVARMAX;
            trackBarNOISEGENRES.Value = (int)sn.NOISECLOCKRESVAR;

            SetResistanceToCombobox(sn.NOISEFILTERRES, cbNOISEFILTERRES);
            trackBarNOISEFILTERRES.Minimum = 0;
            trackBarNOISEFILTERRES.Maximum = (int)sn.NOISEFILTERRESVARMAX;
            trackBarNOISEFILTERRES.Value = (int)sn.NOISEFILTERRESVAR;
            SetCapacitanceToCombobox(sn.NOISEFILTERCAP, cbNOISEFILTERCAP);
            DisplayNoiseData(sn);

            SetResistanceToCombobox(sn.ONESHOTRES, cbONESHOTRES);
            trackBarONESHOTRES.Minimum = 0;
            trackBarONESHOTRES.Maximum = (int)sn.ONESHOTRESVARMAX;
            trackBarONESHOTRES.Value = (int)sn.ONESHOTRESVAR;
            SetCapacitanceToCombobox(sn.ONESHOTCAP, cbONESHOTCAP);
            DisplayOneShotData(sn);

            SetCapacitanceToCombobox(sn.ENVCAP, cbENVCAP);
            SetResistanceToCombobox(sn.ENVATKRES, cbENVATKRES);
            trackBarENVATKRES.Minimum = 0;
            trackBarENVATKRES.Maximum = (int)sn.ENVATKRESVARMAX;
            trackBarENVATKRES.Value = (int)sn.ENVATKRESVAR;
            SetResistanceToCombobox(sn.ENVDECRES, cbENVDECRES);
            trackBarENVDECRES.Minimum = 0;
            trackBarENVDECRES.Maximum = (int)sn.ENVDECRESVARMAX;
            trackBarENVDECRES.Value = (int)sn.ENVDECRESVAR;
            DisplayEnvelopeData(sn);

            SetResistanceToCombobox(sn.FEEDBACKRES, cbFEEDBACKRES);
            SetResistanceToCombobox(sn.AMPLITUDERES, cbAMPLITUDERES);

            checkBoxMIXVCO.Checked = false;
            checkBoxMIXSLF.Checked = false;
            checkBoxMIXNOISE.Checked = false;

            if (sn.MixerMode == MixerMode.VCO)
            {
                checkBoxMIXVCO.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.SLF)
            {
                checkBoxMIXSLF.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.NOISE)
            {
                checkBoxMIXNOISE.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.VCO_NOISE)
            {
                checkBoxMIXVCO.Checked = true;
                checkBoxMIXNOISE.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.SLF_NOISE)
            {
                checkBoxMIXSLF.Checked = true;
                checkBoxMIXNOISE.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.SLF_VCO_NOISE)
            {
                checkBoxMIXVCO.Checked = true;
                checkBoxMIXSLF.Checked = true;
                checkBoxMIXNOISE.Checked = true;
            }
            else if (sn.MixerMode == MixerMode.SLF_VCO)
            {
                checkBoxMIXVCO.Checked = true;
                checkBoxMIXSLF.Checked = true;
            }

            rbENVVCO.Checked = false;
            rbENVMIXER.Checked = false;
            rbENVONESHOT.Checked = false;
            rbENVVCOALT.Checked = false;

            if (sn.EnvelopeMode == EnvelopeMode.VCO) rbENVVCO.Checked = true;
            else if (sn.EnvelopeMode == EnvelopeMode.VCO_ALTERNATING) rbENVVCOALT.Checked = true;
            else if (sn.EnvelopeMode == EnvelopeMode.MIXER) rbENVMIXER.Checked = true;
            else if (sn.EnvelopeMode == EnvelopeMode.ONESHOT) rbENVONESHOT.Checked = true;
        }

        #endregion


        #region ToolbarCommands

        private void toolStripButtonNew_Click(object sender, EventArgs e)
        {
            _sn = new SN76477();
            _currentFilename = "<new>";
            DisplayConfiguration(_sn);
        }

        private SN76477 LoadFile(string filename)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SN76477));
                StreamReader reader = File.OpenText(filename);
                SN76477 sn = (SN76477)serializer.Deserialize(reader);
                reader.Close();
                return sn;
            }
            catch
            {
                return null;
            }
        }

        private void toolStripButtonOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;
            ofd.DefaultExt = "xml";
            if (ofd.ShowDialog(this) == DialogResult.OK) _currentFilename = ofd.FileName;

            SN76477 sn = this.LoadFile(_currentFilename);
            if (sn != null)
            {
                _sn = sn;
                DisplayConfiguration(_sn);
                DisplayApplicationTitle();
            }
        }

        private void toolStripButtonSave_Click(object sender, EventArgs e)
        {
            if (_currentFilename == "<new>")
            {
                SaveFileDialog ofd = new SaveFileDialog();
                ofd.CheckPathExists = true;
                ofd.DefaultExt = "xml";
                if (ofd.ShowDialog(this) == DialogResult.OK) _currentFilename = ofd.FileName;
                DisplayApplicationTitle();

            }

            if (_currentFilename != "<new>")
            {

                XmlSerializer serializer = new XmlSerializer(typeof(SN76477));
                StreamWriter writer = File.CreateText(_currentFilename);
                serializer.Serialize(writer, _sn);
                writer.Close();
                DisplayApplicationTitle();
            }
        }

        private void toolStripButtonPlay_Click(object sender, EventArgs e)
        {
            _wavPlayer.Stop();

            _sn.GenerateSamples(44100 * 3, _wav_file_standard);

            _wavPlayer.SoundLocation = _wav_file_standard;
            _wavPlayer.PlayLooping();
        }

        private void toolStripButtonPause_Click(object sender, EventArgs e)
        {
            _wavPlayer.Stop();
        }

        private void toolStripButtonBufferPlay_Click(object sender, EventArgs e)
        {
            toolStripButtonBufferPlay.Checked = !toolStripButtonBufferPlay.Checked;

        }

        private void toolStripButtonBufferPlay_CheckStateChanged(object sender, EventArgs e)
        {
            if (!_bufferPlaying)
            {
                if (toolStripButtonBufferPlay.Checked) StartPlay();
            }
            else
            {
                if (!toolStripButtonBufferPlay.Checked) StopPlay();
            }
        }

        #endregion

        private void trackBarNOISEGENRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.NOISECLOCKRESVAR = trackBarNOISEGENRES.Value;
                DisplayNoiseData(_sn);
            }
        }

        private void trackBarNOISEFILTERRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.NOISEFILTERRESVAR = trackBarNOISEFILTERRES.Value;
                DisplayNoiseData(_sn);
            }
        }

        private void trackBarONESHOTRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ONESHOTRESVAR = trackBarONESHOTRES.Value;
                DisplayOneShotData(_sn);
            }
        }

        private void trackBarENVATKRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ENVATKRESVAR = trackBarENVATKRES.Value;
                DisplayEnvelopeData(_sn);
            }
        }

        private void trackBarENVDECRES_Scroll(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.ENVDECRESVAR = trackBarENVDECRES.Value;
                DisplayEnvelopeData(_sn);
            }
        }

        private void cbAMPLITUDERES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.AMPLITUDERES = GetResistanceFromCombobox(cbAMPLITUDERES);
                DisplayOutputData(_sn);
            }
        }

        private void cbFEEDBACKRES_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sn != null)
            {
                _sn.FEEDBACKRES = GetResistanceFromCombobox(cbFEEDBACKRES);
                DisplayOutputData(_sn);
            }
        }



    }
}
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PacketViewerLogViewer
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SaveButtonsIntoColorSettings();
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            LoadColorSettingsIntoButtons();
        }

        private void LoadColorSettingsIntoButtons()
        {
            // Manual loading
            btnBackIN.BackColor = Properties.Settings.Default.ColBackIN;
            btnBackOUT.BackColor = Properties.Settings.Default.ColBackOUT;
            btnBackUNK.BackColor = Properties.Settings.Default.ColBackUNK;
            btnBarIN.BackColor = Properties.Settings.Default.ColBarIN;
            btnBarOUT.BackColor = Properties.Settings.Default.ColBarOUT;
            btnBarUNK.BackColor = Properties.Settings.Default.ColBarUNK;
            btnFontIN.BackColor = Properties.Settings.Default.ColFontIN;
            btnFontOUT.BackColor = Properties.Settings.Default.ColFontOUT;
            btnFontUNK.BackColor = Properties.Settings.Default.ColFontUNK;
            btnSelectedFontIN.BackColor = Properties.Settings.Default.ColSelectedFontIN;
            btnSelectedFontOUT.BackColor = Properties.Settings.Default.ColSelectedFontOUT;
            btnSelectedFontUNK.BackColor = Properties.Settings.Default.ColSelectedFontUNK;
            btnSelectIN.BackColor = Properties.Settings.Default.ColSelectIN;
            btnSelectOUT.BackColor = Properties.Settings.Default.ColSelectOUT;
            btnSelectUNK.BackColor = Properties.Settings.Default.ColSelectUNK;
            btnSyncIN.BackColor = Properties.Settings.Default.ColSyncIN;
            btnSyncOUT.BackColor = Properties.Settings.Default.ColSyncOUT;
            btnSyncUNK.BackColor = Properties.Settings.Default.ColSyncUNK;
        }

        private void SaveButtonsIntoColorSettings()
        {

            // Manual loading
            Properties.Settings.Default.ColBackIN = btnBackIN.BackColor;
            Properties.Settings.Default.ColBackOUT = btnBackOUT.BackColor;
            Properties.Settings.Default.ColBackUNK = btnBackUNK.BackColor;
            Properties.Settings.Default.ColBarIN = btnBarIN.BackColor;
            Properties.Settings.Default.ColBarOUT = btnBarOUT.BackColor;
            Properties.Settings.Default.ColBarUNK = btnBarUNK.BackColor;
            Properties.Settings.Default.ColFontIN = btnFontIN.BackColor;
            Properties.Settings.Default.ColFontOUT = btnFontOUT.BackColor;
            Properties.Settings.Default.ColFontUNK = btnFontUNK.BackColor;
            Properties.Settings.Default.ColSelectedFontIN = btnSelectedFontIN.BackColor;
            Properties.Settings.Default.ColSelectedFontOUT = btnSelectedFontOUT.BackColor;
            Properties.Settings.Default.ColSelectedFontUNK = btnSelectedFontUNK.BackColor;
            Properties.Settings.Default.ColSelectIN = btnSelectIN.BackColor;
            Properties.Settings.Default.ColSelectOUT = btnSelectOUT.BackColor;
            Properties.Settings.Default.ColSelectUNK = btnSelectUNK.BackColor;
            Properties.Settings.Default.ColSyncIN = btnSyncIN.BackColor;
            Properties.Settings.Default.ColSyncOUT = btnSyncOUT.BackColor;
            Properties.Settings.Default.ColSyncUNK = btnSyncUNK.BackColor;
        }

        private void btnDefault_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reset();
            LoadColorSettingsIntoButtons();
        }

        private void btnColorButton_Click(object sender, EventArgs e)
        {
            var btn = (sender as Button);
            colorDlg.Color = btn.BackColor;
            if (colorDlg.ShowDialog() == DialogResult.OK)
            {
                btn.BackColor = colorDlg.Color;
            }
        }
    }
}

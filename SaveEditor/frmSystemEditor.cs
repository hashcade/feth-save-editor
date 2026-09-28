using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using SaveEditor.Structs;

namespace SaveEditor
{
    public partial class frmSystemEditor : Form
    {
        private SystemSave save;
        public enmLanguage currentLanguage;
		private string curFile;
		private bool IsLoading, IsUpdating;

        public frmSystemEditor()
        {
            InitializeComponent();
        }

        private void frmSystemEditor_Load(object sender, EventArgs e)
        {

        }

        private void frmSystemEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void frmSystemEditor_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            curFile = files[0];
            LoadSave(curFile);
        }
        
        private void mnuLoadSave_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.CheckFileExists = true;
                ofd.Title = @"Load Save File";
                ofd.Filter = @"Fire Emblem System Save Files|system|All Files|*.*";
                ofd.InitialDirectory = Application.StartupPath;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    curFile = ofd.FileName;
                    LoadSave(curFile);
                }
            }
        }

        private void mnuWriteSave_Click(object sender, EventArgs e)
        {
            if (save == null)
            {
                MessageBox.Show(@"You can't write a save, before loading it.", @"No save loaded!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = @"Write Save File";
                sfd.Filter = @"Fire Emblem System Save Files|system|All Files|*.*";
                sfd.FileName = Path.GetFileName(curFile);

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    WriteSave(sfd.FileName);
                }
            }
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
             Close();
        }

        private void LoadSave(string sPath)
		{
			IsLoading = true;
			
            try
            {
                long fSize = Util.GetFileSize(sPath);

                save = new SystemSave();

                if (fSize == SystemSave.SIZE_SAVE_V5)
                {
                    save.Read(sPath);
                }
                else if (fSize == SystemSave.SIZE_SAVE_V7)
                {
                    save.Read(sPath);
                }
                else
                {
                    throw new NotSupportedException("This file is not supported, invalid filesize!");
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                IsLoading = false;
                return;
            }

            if (save.WarnChecksum)
            {
                MessageBox.Show($@"Calculated: 0x{save.CalculatedChecksum:X} != Read: 0x{save.Checksum:X}", @"Invalid Checksum", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (save.WarnVersionUpdate)
            {
                string updateMessage = $@"Your 'system' save file was updated to version {SystemSave.CURRENT_VERSION}!";
                updateMessage += "\r\n";
                updateMessage += "You can only use it with v1.1.0 and higher after saving it!";

                MessageBox.Show(updateMessage, @"Version Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            ChangeLanguage();
			
			IsLoading = false;
		}
        
        private void WriteSave(string sPath)
        {
            if(save == null)
                return;

            save.Write(sPath);
        }

        private void ChangeLanguage()
        {
            InitComboAndChecklists();
            FillForm();
        }

        private void InitComboAndChecklists()
        {
            chklstFlags.Items.Clear();

            string temp_name;

            for (int i = 0; i < SystemSaveData_V7.COUNT_FLAGS; i++)
            {
                temp_name = "";

                if(i >= 8 && i < 108)
                {
                    temp_name = "{MOVIE} " + Database.GetString(12823 + (i - 8));
                }

                chklstFlags.Items.Add($"[{i:D4}] {temp_name}");
            }
        }

        private void FillForm()
        {
            lstSaveSlot.Items.Clear();

            for(int i = 0;i<save.SaveData.Infos.Length;i++)
            {
                var info = save.SaveData.Infos[i];
                lstSaveSlot.Items.Add($"[{i:D2}] {info.GetPlayerName()} - {info.GetPlaytime()}");
            }

            lstSaveSlot.SelectedIndex = 0;

            Util.FillFlagTableArray(chklstFlags, save.SaveData.Flags);
        }









    }
}

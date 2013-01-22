namespace AdvancedClipboard
{
	partial class CodePreview
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.scintilla1 = new ScintillaNET.Scintilla();
			((System.ComponentModel.ISupportInitialize)(this.scintilla1)).BeginInit();
			this.SuspendLayout();
			// 
			// scintilla1
			// 
			this.scintilla1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.scintilla1.Location = new System.Drawing.Point(0, 0);
			this.scintilla1.Name = "scintilla1";
			this.scintilla1.Size = new System.Drawing.Size(564, 475);
			this.scintilla1.TabIndex = 0;
			// 
			// CodePreview
			// 
			this.AllowDrop = true;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(564, 475);
			this.Controls.Add(this.scintilla1);
			this.DoubleBuffered = true;
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.Name = "CodePreview";
			this.Opacity = 0.95D;
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "CodePreview";
			this.TopMost = true;
			this.Load += new System.EventHandler(this.CodePreview_Load);
			((System.ComponentModel.ISupportInitialize)(this.scintilla1)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		public ScintillaNET.Scintilla scintilla1;
	}
}
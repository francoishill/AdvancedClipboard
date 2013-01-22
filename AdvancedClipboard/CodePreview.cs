using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdvancedClipboard
{
	public partial class CodePreview : Form
	{
		public CodePreview()
		{
			InitializeComponent();
		}

		protected override bool ShowWithoutActivation
		{
			get
			{
				return true;//base.ShowWithoutActivation;
			}
		}

		private void CodePreview_Load(object sender, EventArgs e)
		{

		}
	}
}

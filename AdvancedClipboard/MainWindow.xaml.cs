using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using SharedClasses;
using System.Windows.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WindowsInput;
using WindowsInput.Native;
using System.ComponentModel;

namespace AdvancedClipboard
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const bool cDefaultTopmost = true;
		private const bool cDefaultShowInTaskbar = true;

		private bool monitorCheckboxChange_KeepOnTop = false;
		private bool monitorCheckboxChange_ShowTaskbar = false;

		private readonly TimeSpan checkForegroundInterval = TimeSpan.FromMilliseconds(500);
		//private readonly string cCodeSnippetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),  @"FJH\AdvancedClipboard\CodeSnippets");
		private const string cSnippetExtension = ".fsnip";
		private readonly string cSettingpathTopmostBool = SettingsInterop.GetFullFilePathInLocalAppdata("TopmostBool", TextGroup.cThisAppName, "Settings");
		private readonly string cSettingpathShowintaskbarBool = SettingsInterop.GetFullFilePathInLocalAppdata("ShowInTaskbarBool", TextGroup.cThisAppName, "Settings");

		private CodePreview codePreview;
		private ObservableCollection<CodeSnippet> listOfSnippets = new ObservableCollection<CodeSnippet>();
		private ObservableCollection<TextGroup> listOfTextGroups = new ObservableCollection<TextGroup>();
		private bool hasTwoOrMoreScreens = false;
		ObservableCollection<CodeSnippet> unfilteredListOfSnippets = new ObservableCollection<CodeSnippet>();

		public MainWindow()
		{
			InitializeComponent();

			this.Topmost = LoadSettingTopmostBool();
			this.ShowInTaskbar = LoadSettingShowInTaskbarBool();

			monitorCheckboxChange_KeepOnTop = true;
			monitorCheckboxChange_ShowTaskbar = true;

			if (System.Windows.Forms.Screen.AllScreens.Length > 1)
				hasTwoOrMoreScreens = true;

			//PopulateAllSnippetsList();
			treeviewSnippets.ItemsSource = listOfSnippets;
			tabcontrolTextGroups.ItemsSource = listOfTextGroups;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.Left = WorkingArea.Right - this.Width;
			this.Top = WorkingArea.Top;
			this.Height = WorkingArea.Height;
			StartForegroundCheckTimer();

			statusbaritemTextblockQuickInfoHoverForTooltip.ToolTip =
				  "Alt + Shift + `   :   Scroll through Group Tabs" + Environment.NewLine
				+ "Alt + Shift + -   :   Enable/Disable application" + Environment.NewLine
				+ "Alt + Shift + 1-0 :   Copy the current text in the active window into the corresponding item (1-0)" + Environment.NewLine
				+ "Alt + Ctrl + 1-0  :   Paste the corresponding item (1, 2, 3 ... 9, 0) into the current active window";

			List<string> couldNotRegisterHotkeysList = new List<string>();
			if (!Win32Api.RegisterHotKey(this.GetHandle(), Win32Api.Hotkey1, Win32Api.MOD_ALT + Win32Api.MOD_SHIFT, (uint)System.Windows.Forms.Keys.Oemtilde))
				couldNotRegisterHotkeysList.Add("Alt + Shift + `");
			if (!Win32Api.RegisterHotKey(this.GetHandle(), Win32Api.Hotkey2, Win32Api.MOD_ALT + Win32Api.MOD_SHIFT, (uint)System.Windows.Forms.Keys.OemMinus))
				couldNotRegisterHotkeysList.Add("Alt + Shift + -");
			foreach (var keynum in TextGroup.cKeyBindings.Keys)
			{
				//Paste
				if (!Win32Api.RegisterHotKey(this.GetHandle(), Win32Api.MultipleHotkeyStart + keynum, Win32Api.MOD_CONTROL + Win32Api.MOD_ALT, (uint)TextGroup.cKeyBindings[keynum]))
					couldNotRegisterHotkeysList.Add("Ctrl + Alt + " + keynum);
				//Copy to
				if (!Win32Api.RegisterHotKey(this.GetHandle(), Win32Api.MultipleHotkeyStart + TextGroup.cKeyBindings.Count + keynum, Win32Api.MOD_ALT + Win32Api.MOD_SHIFT, (uint)TextGroup.cKeyBindings[keynum]))
					couldNotRegisterHotkeysList.Add("Alt + Shift + " + keynum);
			}
			if (couldNotRegisterHotkeysList.Count > 0)
				UserMessages.ShowWarningMessage(
					"The following hotkeys could not be registered (this might be because another application is using them)." + Environment.NewLine
					+ string.Join(Environment.NewLine, couldNotRegisterHotkeysList));

			couldNotRegisterHotkeysList.Clear();
			couldNotRegisterHotkeysList = null;

			RepopulateTabs();

			this.UpdateLayout();
			try
			{
				codePreview = new CodePreview();
			}
			catch (Exception exc)
			{
				UserMessages.ShowErrorMessage(exc.Message);
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
			source.AddHook(WndProc);
		}

		private bool HotkeysActive = true;
		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			try
			{
				if (msg == Win32Api.WM_HOTKEY)
				{
					if (wParam == new IntPtr(Win32Api.Hotkey1))
					{
						if (tabcontrolTextGroups.Items.Count > 1)
						{
							int ind = tabcontrolTextGroups.SelectedIndex;
							if (-1 == ind)
								tabcontrolTextGroups.SelectedIndex = 0;
							else if (ind < tabcontrolTextGroups.Items.Count - 1)
								tabcontrolTextGroups.SelectedIndex++;
							else
								tabcontrolTextGroups.SelectedIndex = 0;

							GetListboxOfSelectedTabItem().SelectedItem = null;
						}
					}
					else if (wParam == new IntPtr(Win32Api.Hotkey2))
					{
						HotkeysActive = !HotkeysActive;
						tabcontrolTextGroups.IsEnabled = HotkeysActive;
					}
					else
					{
						if (HotkeysActive)
						{
							foreach (var keynum in TextGroup.cKeyBindings.Keys)
								if (wParam == new IntPtr(Win32Api.MultipleHotkeyStart + keynum))
								{
									int filenameNumber = keynum == 0 ? 9 : keynum - 1;

									bool success = false;
									TextGroup tg = tabcontrolTextGroups.SelectedItem as TextGroup;
									if (tg != null)
									{
										success = true;
										handled = true;
										PasteTextInActiveWindow(tg.GroupTextList[filenameNumber]);
									}
									/*if (tabcontrolTextGroups.SelectedItem is TextGroup)
									{
										var tmpdict = tabcontrolTextGroups.SelectedItem.Tag as Dictionary<int, string>;
										if (tmpdict != null)
										{
											success = true;
											var filepath = tmpdict[keynum];
											if (!File.Exists(filepath) || string.IsNullOrWhiteSpace(File.ReadAllText(filepath)))
												UserMessages.ShowWarningMessage("Empty item assigned to hotkey Ctrl + " + keynum);
											else
											{
												PasteTextInActiveWindow(File.ReadAllText(filepath));
											}
										}
									}*/
									if (!success)
										UserMessages.ShowWarningMessage("Could not perform hotkey procedure");
								}
								else if (wParam == new IntPtr(Win32Api.MultipleHotkeyStart + TextGroup.cKeyBindings.Count + keynum))
								{
									int filenameNumber = keynum == 0 ? 9 : keynum - 1;

									bool success = false;
									TextGroup tg = tabcontrolTextGroups.SelectedItem as TextGroup;
									if (tg != null)
									{
										success = true;
										handled = true;
										//UserMessages.ShowInfoMessage("Changing item " + keynum + " to " + CopySelectedTextOfActiveWindow());
										string tmpSelectedText;
										bool copySuccess = CopySelectedTextOfActiveWindow(out tmpSelectedText);
										if (copySuccess)
										{
											tg[filenameNumber] = new TextGroup.TextItem(tmpSelectedText);
											/*File.WriteAllText(tg.GetNumberFilename(filenameNumber), tmpSelectedText);
											RepopulateTabs();*/
										}
									}
									if (!success)
										UserMessages.ShowWarningMessage("Could not perform hotkey procedure");
								}
						}
					}
				}
			}
			catch (Exception exc)
			{
				UserMessages.ShowErrorMessage("WndProc error: " + exc.Message);
			}
			return IntPtr.Zero;
		}

		private static bool LoadBooleanSetting(string filePath, string settingDisplayName, bool defaultValue)
		{
			try
			{
				if (!File.Exists(filePath))
				{
					File.WriteAllText(filePath, defaultValue.ToString());
					return defaultValue;
				}
				else
				{
					string fileContent = File.ReadAllText(filePath) ?? "";
					bool tmpbool;
					if (!bool.TryParse(fileContent, out tmpbool))
					{
						UserMessages.ShowWarningMessage(string.Format("Unable to obtain setting '{0}', using default = {1}. File content was: {2}", settingDisplayName, defaultValue, fileContent));
						return defaultValue;
					}
					else
						return tmpbool;
				}
			}
			catch (Exception exc)
			{
				UserMessages.ShowErrorMessage(string.Format("Error read setting '{0}', using default = {1}. Error message: {2}", settingDisplayName, defaultValue, exc.Message));
				return defaultValue;
			}
		}

		private static void SaveBooleanSetting(string filePath, string settingDisplayName, bool newValue)
		{
			try
			{
				File.WriteAllText(filePath, newValue.ToString());
			}
			catch (Exception exc)
			{
				UserMessages.ShowErrorMessage(string.Format("Error saving setting '{0}', error message: {1}", settingDisplayName, exc.Message));
			}
		}

		private bool LoadSettingTopmostBool()
		{
			return LoadBooleanSetting(cSettingpathTopmostBool, "Keep on top", cDefaultTopmost);
		}

		private bool LoadSettingShowInTaskbarBool()
		{
			return LoadBooleanSetting(cSettingpathShowintaskbarBool, "Show Taskbar icon", cDefaultShowInTaskbar);
		}

		private void SaveSettingTopmostBool(bool newValue)
		{
			SaveBooleanSetting(cSettingpathTopmostBool, "Keep on top", newValue);
		}

		private void SaveSettingShowInTaskbarBool(bool newValue)
		{
			SaveBooleanSetting(cSettingpathShowintaskbarBool, "Show Taskbar icon", newValue);
		}

		private void RepopulateTabs()
		{
			string lastSelectedTabName = null;
			TextGroup selectedTab = tabcontrolTextGroups.SelectedItem as TextGroup;
			if (selectedTab != null)
				lastSelectedTabName = selectedTab.GroupName;

			listOfTextGroups.Clear();

			foreach (var tg in TextGroup.GetCurrentList())
				listOfTextGroups.Add(tg);

			if (listOfTextGroups.Count == 0)
			{
				UserMessages.ShowWarningMessage("There are no groups yet, you will now be prompted to choose a group name.");
				var tg = TextGroup.PromptForNewTextGroup();
				if (tg != null)
					listOfTextGroups.Add(tg);
			}

			if (lastSelectedTabName != null)
			{
				var itemsWithSameGroupName = listOfTextGroups.Where(tg => tg.GroupName.Equals(lastSelectedTabName)).ToArray();
				if (itemsWithSameGroupName.Length > 0)
					tabcontrolTextGroups.SelectedItem = itemsWithSameGroupName[0];
			}
			else if (listOfTextGroups.Count > 0)
				tabcontrolTextGroups.SelectedIndex = 0;

			/*foreach (var group in TextGroupList)
			{
				tabControl1.TabPages.Add(groupname, groupname);
				var tab = tabControl1.TabPages[groupname];
				tab.Tag = groupedCodeSnippets[groupname];
				TreeView tv = new TreeView();
				tv.ShowNodeToolTips = true;
				tv.ShowLines = false;
				tv.ShowPlusMinus = false;
				tv.ShowRootLines = false;
				tv.FullRowSelect = true;
				StylingInterop.SetTreeviewVistaStyle(tv);
				tv.Dock = DockStyle.Fill;
				foreach (var key in groupedCodeSnippets[groupname].Keys)
				{
					var filetext = "";
					if (File.Exists(groupedCodeSnippets[groupname][key]))
						filetext = File.ReadAllText(groupedCodeSnippets[groupname][key]);
					var tmpstr = key + ": " + filetext;
					TreeNode tn = new TreeNode(tmpstr) { Name = tmpstr, ToolTipText = tmpstr };
					tv.Nodes.Add(tn);
				}
				tab.Controls.Add(tv);
			}*/
		}

		/*private void PopulateAllSnippetsList()//ApplicationTypes apptype = ApplicationTypes.CSharp)
		{
			allSnippets.Clear();
			CodeSnippetList.Clear();
			treeviewSnippets.SelectedItem = null;

			if (!Directory.Exists(cCodeSnippetDir))
				Directory.CreateDirectory(cCodeSnippetDir);
			foreach (string snipFile in Directory.GetFiles(cCodeSnippetDir, "*" + cSnippetExtension, SearchOption.TopDirectoryOnly))
			{
				string fileContents = File.ReadAllText(snipFile);
				if (string.IsNullOrWhiteSpace(fileContents))
				{
					UserMessages.ShowErrorMessage("File content is incorrect format, file " + snipFile + ", content: " + Environment.NewLine + fileContents);
					continue;
				}

				int newLinePos = -1;
				int newLineLength = 0;

				if (newLinePos == -1)
				{
					newLinePos = fileContents.IndexOf("\r\n");
					newLineLength = 2;
				}
				if (newLinePos == -1)
				{
					newLinePos = fileContents.IndexOf("\n\r");
					newLineLength = 2;
				}
				if (newLinePos == -1)
				{
					newLinePos = fileContents.IndexOf("\n");
					newLineLength = 1;
				}

				if (newLinePos == -1)
				{
					UserMessages.ShowErrorMessage("File content is incorrect format, file " + snipFile + ", content: " + Environment.NewLine + fileContents);
					continue;
				}

				string firstLine = fileContents.Substring(0, newLinePos);
				string[] ApptypeAndDisplayname = firstLine.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
				if (ApptypeAndDisplayname.Length != 2)
				{
					UserMessages.ShowErrorMessage("File first line is incorrect format, file " + snipFile + ", first line: \"" + firstLine + "\"");
					continue;
				}

				string snippetContent = fileContents.Substring(newLinePos + newLineLength);
				if (string.IsNullOrWhiteSpace(snippetContent))
					UserMessages.ShowWarningMessage("Snippet content is empty, full file contents for file " + snipFile + ": " + Environment.NewLine + fileContents);

				ApplicationTypes newapptype;
				if (!Enum.TryParse(ApptypeAndDisplayname[0], true, out newapptype))
				{
					UserMessages.ShowErrorMessage("Application type on first line cannot be parsed, file " + snipFile + ", application type string: \"" + ApptypeAndDisplayname[0] + "\"");
					continue;
				}
				allSnippets.Add(new CodeSnippet(newapptype, ApptypeAndDisplayname[1], snippetContent));
			}

			//            allSnippets.Add(new CodeSnippet(ApplicationTypes.CSharp, "Test1csharp",
			//@"using System.Text;
			//using System.Windows.Forms;"));
			currentApplicationType = ApplicationTypes.None;//apptype;
			SetFilterOnApplicationList(currentApplicationType);
		}*/

		private void PasteTextInActiveWindow(TextGroup.TextItem textItem)
		{
		//try
		//{
		/*ThreadingInterop.ActionAfterDelay(
			delegate
			{*/

		retrySetClipboard:
			int maxRetries = 3;
			int retriedCount = 0;
			try
			{
				Clipboard.SetText(textItem.Text);
			}
			catch (Exception exc)
			{
				if (++retriedCount < maxRetries)
				{
					Thread.Sleep(500);
					goto retrySetClipboard;
				}
				else
				{
					UserMessages.ShowErrorMessage("Unable to paste text, retried already " + maxRetries + " times, error message: " + exc.Message);
					return;
				}
			}


			//System.Windows.Forms.SendKeys.SendWait("^v");

			//bool wasControlDown =  Convert.ToBoolean(Win32Api.GetKeyState(Win32Api.VirtualKeyStates.VK_LCONTROL) & Win32Api.KEY_PRESSED);
			//bool wasAltDown = Convert.ToBoolean(Win32Api.GetKeyState(Win32Api.VirtualKeyStates.VK_MENU) & Win32Api.KEY_PRESSED);

			//if (wasControlDown)//Release it first
			//    Win32Api.keybd_event(Win32Api.VK_LCONTROL, 0x45, Win32Api.KEYEVENTF_KEYUP, 0);
			//if (wasAltDown)
			//    Win32Api.keybd_event(Win32Api.VK_MENU, 0x45, Win32Api.KEYEVENTF_KEYUP, 0);

			//Win32Api.keybd_event(Win32Api.VK_OEM_5, 0x45, Win32Api.KEYEVENTF_KEYUP, 0);

			var sim = new KeyboardSimulator();
			sim.TextEntry(textItem.Text);
			//sim.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);

			/*Win32Api.keybd_event(Win32Api.VK_LCONTROL, 0x45, 0, 0);
			Win32Api.keybd_event(Win32Api.VK_V, 0x45, 0, 0);
			Win32Api.keybd_event(Win32Api.VK_V, 0x45, Win32Api.KEYEVENTF_KEYUP, 0);
			Win32Api.keybd_event(Win32Api.VK_LCONTROL, 0x45, Win32Api.KEYEVENTF_KEYUP, 0);*/

			//if (wasControlDown)
			//    Win32Api.keybd_event(Win32Api.VK_LCONTROL, 0x45, 0, 0);
			//if (wasAltDown)
			//    Win32Api.keybd_event(Win32Api.VK_MENU, 0x45, 0, 0);

			/*},
			TimeSpan.FromMilliseconds(500),
			err => UserMessages.ShowErrorMessage(err));*/
			/*Console.WriteLine("Before settings Clipboard.Text = " + text);
			Clipboard.SetText(text);
			Console.WriteLine("After settings Clipboard.Text, it is now = " + Clipboard.GetText());
			System.Windows.Forms.SendKeys.SendWait("^v");
			System.Windows.Forms.SendKeys.Flush();*/
			//}
			//catch (Exception exc)
			//{
			//    UserMessages.ShowWarningMessage("Cannot paste: " + exc.Message);
			//}
		}

		private bool CopySelectedTextOfActiveWindow(out string textIfSucceeded)
		{
			var sim = new KeyboardSimulator();

			var adap = new WindowsInputDeviceStateAdaptor();
			bool wasShiftDown = adap.IsKeyDown(VirtualKeyCode.SHIFT);
			bool wasAltDown = adap.IsKeyDown(VirtualKeyCode.MENU);

			if (wasShiftDown)
				sim.KeyUp(VirtualKeyCode.SHIFT);
			if (wasAltDown)
				sim.KeyUp(VirtualKeyCode.MENU);


			//sim.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);

		retryClearClipboard:
			int maxRetries = 3;
			int retriedCount = 0;
			try
			{
				Clipboard.Clear();
			}
			catch (Exception exc)
			{
				if (++retriedCount < maxRetries)
				{
					Thread.Sleep(500);
					goto retryClearClipboard;
				}
				else
				{
					UserMessages.ShowErrorMessage("Unable to Clear Clipboard, retried already " + maxRetries + " times, error message: " + exc.Message);
					textIfSucceeded = null;
					return false;
				}
			}

			sim.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);
			//Thread.Sleep(50);
			sim.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);//Yes we do it again, it sometimes fail after the first time

			/*sim.KeyDown(VirtualKeyCode.CONTROL);
			sim.KeyPress(VirtualKeyCode.VK_C);
			sim.KeyUp(VirtualKeyCode.CONTROL);*/

			//System.Windows.Forms.SendKeys.SendWait("^(c)");

			if (wasShiftDown)
				sim.KeyDown(VirtualKeyCode.SHIFT);
			if (wasAltDown)
				sim.KeyDown(VirtualKeyCode.MENU);
			System.Windows.Forms.SendKeys.Flush();

		retryGetClipboard:
			maxRetries = 3;
			retriedCount = 0;
			try
			{
				string copiedText = Clipboard.GetText();
				if (string.IsNullOrEmpty(copiedText))
				{
					Thread.Sleep(500);
					sim.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);//Yes we do it even a third time, it sometimes fail after the first two times??? WHY???
					copiedText = Clipboard.GetText();
				}

				/*if (string.IsNullOrEmpty(copiedText) && Clipboard.ContainsImage())
				{
				}*/

				textIfSucceeded = copiedText;
				return true;
			}
			catch (Exception exc)
			{
				if (++retriedCount < maxRetries)
				{
					Thread.Sleep(500);
					goto retryGetClipboard;
				}
				else
				{
					UserMessages.ShowErrorMessage("Unable to copy text, retried already " + maxRetries + " times, error message: " + exc.Message);
					textIfSucceeded = null;
					return false;
				}
			}
		}

		System.Drawing.Rectangle workingArea = new System.Drawing.Rectangle(-1, -1, -1, -1);
		private System.Drawing.Rectangle WorkingArea
		{
			get
			{
				if (workingArea.Height != -1)
					return workingArea;
				workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
				if (hasTwoOrMoreScreens)
					workingArea = System.Windows.Forms.Screen.AllScreens[1].WorkingArea;
				return workingArea;
			}
		}
		System.Windows.Forms.Timer timerForegroundCheck;
		private void StartForegroundCheckTimer()
		{
			if (timerForegroundCheck == null)
				timerForegroundCheck = new System.Windows.Forms.Timer();
			timerForegroundCheck.Interval = (int)checkForegroundInterval.TotalMilliseconds;
			timerForegroundCheck.Tick += delegate
			{
				IntPtr h = Win32Api.GetForegroundWindow();
				uint processId;
				uint threadId = Win32Api.GetWindowThreadProcessId(h, out processId);
				try
				{
					Process proc = Process.GetProcessById((int)processId);
					if (proc == null) return;
					if (proc.Id == Process.GetCurrentProcess().Id) return;

					if (proc.MainModule.FileName.EndsWith("bds.exe", StringComparison.InvariantCultureIgnoreCase))
						SetFilterOnApplicationList(ApplicationTypes.Delphi);
					else if (proc.MainModule.FileName.EndsWith("vcsexpress.exe", StringComparison.InvariantCultureIgnoreCase)
						|| proc.MainModule.FileName.EndsWith("devenv.exe", StringComparison.InvariantCultureIgnoreCase))
						SetFilterOnApplicationList(ApplicationTypes.CSharp);
					else
						SetFilterOnApplicationList(ApplicationTypes.None);
				}
				catch { }//Must maybe show exception message??
			};
			timerForegroundCheck.Start();
		}

		private ApplicationTypes currentApplicationType = ApplicationTypes.None;
		private void SetFilterOnApplicationList(ApplicationTypes applicationType)
		{
			if (applicationType == currentApplicationType) return;

			if (BusyDragging) return;
			if (Mouse.RightButton == MouseButtonState.Pressed || isContextMenuOpen) return;

			listOfSnippets.Clear();
			var filteredList = unfilteredListOfSnippets.Where(snip => snip.ApplicationType == applicationType);
			foreach (CodeSnippet snippet in filteredList)
				AddTreeNode(snippet);
			statusbarItemStatus.Content = "Active application: " + applicationType.ToString();
			currentApplicationType = applicationType;
		}

		private void AddTreeNode(CodeSnippet snippet)
		{
			listOfSnippets.Add(snippet);
		}

		private ListBox GetListboxOfSelectedTabItem()
		{
			ContentPresenter cp = tabcontrolTextGroups.Template.FindName("PART_SelectedContentHost", tabcontrolTextGroups) as ContentPresenter;
			return tabcontrolTextGroups.ContentTemplate.FindName("listboxGroupItemTextList", cp) as ListBox;
		}

		private bool BusyDragging = false;
		private void treeviewSnippets_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			//UserMessages.ShowInfoMessage("Not implemented 'treeviewSnippets_PreviewMouseDown' yet, see AdvancedClipboard project");
		}

		private bool isContextMenuOpen = false;
		private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			isContextMenuOpen = true;
		}

		private void ContextMenu_ContextMenuClosing(object sender, ContextMenuEventArgs e)
		{
			isContextMenuOpen = false;
		}

		private Dictionary<CodeSnippet, bool> isMouseInside = new Dictionary<CodeSnippet, bool>();
		private void borderSnippetItem_MouseEnter(object sender, MouseEventArgs e)
		{
			//Point hoverLocation = treeView1.PointToClient(MousePosition);
			//TreeNode node = treeView1.HitTest(hoverLocation).Node;//e.Location).Node;
			//if (node == null)
			//{
			//    if (codePreview.Visible)
			//    {
			//        //if (BusyDragging)
			//        //    MarkToHideOnMouseUp = true;
			//        //else
			//        HideCodepreview();
			//    }
			//    return;
			//}

			//if (treeView1.SelectedNode != node)
			//    treeView1.SelectedNode = node;

			WPFHelper.DoActionIfObtainedItemFromObjectSender<CodeSnippet>(sender,
				(snipitem) =>
				{
					if (isMouseInside.ContainsKey(snipitem)
						&& isMouseInside[snipitem] == true)
						return;

					if (!isMouseInside.ContainsKey(snipitem))
						isMouseInside.Add(snipitem, true);
					codePreview.scintilla1.ConfigurationManager.Language = snipitem.ApplicationType.ToString().ToLower();
					codePreview.scintilla1.Text = snipitem.Code;

					codePreview.Location = new System.Drawing.Point((int)(this.Left - codePreview.Width), (int)e.GetPosition(this).Y);
					if (!codePreview.Visible)
						AttemptCodepreviewShow();
				});

			//CodeSnippet snippet = node.Tag as CodeSnippet;
			//if (snippet == null) return;
			//Point pointToClient = this.PointToClient(new Point(this.Left, this.Top));
			////codePreview.Location = new Point(this.Left - codePreview.Width, e.Location.Y - pointToClient.Y);
			//codePreview.Location = new Point(this.Left - codePreview.Width, hoverLocation.Y - pointToClient.Y);
			//if (!codePreview.Visible)
			//    AttemptCodepreviewShow();
		}

		private void borderSnippetItem_MouseLeave(object sender, MouseEventArgs e)
		{
			FrameworkElement fe = sender as FrameworkElement;
			if (fe == null) return;
			Console.WriteLine("borderSnippetItem_MouseLeave");
			if (WPFHelper.DoesFrameworkElementContainMouse(fe, 0))//1))
				return;

			WPFHelper.DoActionIfObtainedItemFromObjectSender<CodeSnippet>(sender,
				(snipitem) =>
				{
					if (isMouseInside.ContainsKey(snipitem))
						isMouseInside.Remove(snipitem);
				});

			treeviewSnippets.SelectedItem = null;
			if (codePreview.Visible)
				HideCodepreview();
		}

		private void HideCodepreview()
		{
			//timeFirstShowRequest = DateTime.MaxValue;
			codePreview.Hide();
		}

		private void AttemptCodepreviewShow()
		{
			//if (timeFirstShowRequest == DateTime.MaxValue)
			//{
			//    timeFirstShowRequest = DateTime.Now;
			//    return;
			//}

			//if (DateTime.Now.Subtract(timeFirstShowRequest).TotalMilliseconds > 500)
			//{
			//codePreview.Invoke((Action)delegate
			//{
			codePreview.Show();
			//});
			//}
		}

		private void menuitemSnippetEditCode_Click(object sender, RoutedEventArgs e)
		{
			WPFHelper.DoActionIfObtainedItemFromObjectSender<CodeSnippet>(sender,
				(snipitem) =>
				{
					string inputName = SharedClasses.DialogBoxStuff.InputDialog(
						"Give a name for this snippet for " + currentApplicationType.ToString() + ", snippet text:"
						+ Environment.NewLine + Environment.NewLine + snipitem.Code,
						"Snippet name",
						null,
						300,
						180);
					//InputBoxWPF.Prompt("Enter new code", snipitem.Code
				});
		}

		private void listboxGroupItemTextList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ListBox lb = sender as ListBox;
			if (lb == null) return;
			lb.SelectedItem = null;
		}

		private void checkboxKeepOnTop_Checked(object sender, RoutedEventArgs e)
		{
			if (!monitorCheckboxChange_KeepOnTop) return;
			if (!(sender is CheckBox)) return;
			SaveSettingTopmostBool((sender as CheckBox).IsChecked == true);
		}

		private void checkboxKeepOnTop_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!monitorCheckboxChange_KeepOnTop) return;
			if (!(sender is CheckBox)) return;
			SaveSettingTopmostBool((sender as CheckBox).IsChecked == true);
		}

		private void checkboxShowTaskbarIcon_Checked(object sender, RoutedEventArgs e)
		{
			if (!monitorCheckboxChange_ShowTaskbar) return;
			if (!(sender is CheckBox)) return;
			SaveSettingShowInTaskbarBool((sender as CheckBox).IsChecked == true);
		}

		private void checkboxShowTaskbarIcon_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!monitorCheckboxChange_ShowTaskbar) return;
			if (!(sender is CheckBox)) return;
			SaveSettingShowInTaskbarBool((sender as CheckBox).IsChecked == true);
		}

		private void RemoveGroupsFromList(params TextGroup[] groupItems)
		{
			if (!UserMessages.Confirm("Are you sure you want to delete these group item(s)?", "Delete group item(s)"))
				return;

			string folderNameOnly = DateTime.Now.ToString(@"yyyy-MM-dd a\t HH\hmm_ss_fff");

			string folderPath = SettingsInterop.GetFullFolderPathInLocalAppdata(folderNameOnly, TextGroup.cThisAppName, "_RemovedTextGroups");

			foreach (var gi in groupItems)
			{
				Directory.Move(gi.GroupDirPath, Path.Combine(folderPath, Path.GetFileName(gi.GroupDirPath)));
				listOfTextGroups.Remove(gi);
			}
		}

		private void menuitemRemoveGroupItem_Click(object sender, RoutedEventArgs e)
		{
			WPFHelper.DoActionIfObtainedItemFromObjectSender<TextGroup>(sender,
				(groupitem) =>
				{
					RemoveGroupsFromList(groupitem);
				});
		}

		private void menuitemAddNewGroup_Click(object sender, RoutedEventArgs e)
		{
			var tg = TextGroup.PromptForNewTextGroup();
			if (tg != null)
				listOfTextGroups.Add(tg);
		}

		/*private System.Windows.Controls.TreeView GetTabTreeview(TabItem tab)
		{
			if (tab == null)
				return null;
			if (tab.Controls.Count > 0)
			{
				var treeview = tab.Controls[0] as TreeView;
				if (treeview != null)
					return treeview;
			}
			return null;
		}*/
	}

	public enum ApplicationTypes { None, CSharp, Delphi }
	public class CodeSnippet
	{
		public ApplicationTypes ApplicationType { get; set; }
		public string DisplayName { get; set; }
		public string Code;
		public CodeSnippet(ApplicationTypes ApplicationType, string DisplayName, string Code)
		{
			this.ApplicationType = ApplicationType;
			this.DisplayName = DisplayName;
			this.Code = Code;
		}
		public override string ToString()
		{
			return DisplayName;
		}
	}

	public class TextGroup
	{
		public const string cThisAppName = "AdvancedClipboard";

		public string GroupName { get; private set; }
		public string GroupDirPath { get; private set; }
		public ObservableCollection<TextItem> GroupTextList { get; private set; }

		private const int cMinIndex = 0;
		private const int cMaxIndex = 9;
		public TextItem this[int Index]
		{
			get
			{
				if (Index >= cMinIndex && Index <= cMaxIndex)
				{
					//while (GroupTextList.Count < cMaxIndex)
					//    GroupTextList.Add(new TextItem(""));
					return GroupTextList[Index];
				}
				else
				{
					UserMessages.ShowWarningMessage("Index = " + Index.ToString() + " is not acceptable for class TextGroup");
					return null;
				}
			}
			set
			{
				if (Index >= cMinIndex && Index <= cMaxIndex)
				{
					try
					{
						File.WriteAllText(this.GetNumberFilename(Index), value.Text);
						//while (GroupTextList.Count < cMaxIndex)
						//    GroupTextList.Add(new TextItem(""));
						GroupTextList[Index] = value;
					}
					catch (Exception exc)
					{
						UserMessages.ShowErrorMessage("Error trying to save the copied text: " + exc.Message);
					}
				}
				else
					UserMessages.ShowWarningMessage("Index = " + Index.ToString() + " is not acceptable for class TextGroup");
			}
		}

		public TextGroup(string GroupName)
		{
			this.GroupName = GroupName;
			this.GroupDirPath = GetDirpathFromGroupName(GroupName);
			
			this.GroupTextList = new ObservableCollection<TextItem>();
			while (GroupTextList.Count <= cMaxIndex)
				GroupTextList.Add(new TextItem(""));

			PopulateGroupTextList();
		}

		private string GetDirpathFromGroupName(string groupName)
		{
			return Path.Combine(cTextGroupsDir, groupName);
		}

		public static readonly Dictionary<int, System.Windows.Forms.Keys> cKeyBindings = new Dictionary<int, System.Windows.Forms.Keys>()
		{
			{ 0, System.Windows.Forms.Keys.D0 },
			{ 1, System.Windows.Forms.Keys.D1 },
			{ 2, System.Windows.Forms.Keys.D2 },
			{ 3, System.Windows.Forms.Keys.D3 },
			{ 4, System.Windows.Forms.Keys.D4 },
			{ 5, System.Windows.Forms.Keys.D5 },
			{ 6, System.Windows.Forms.Keys.D6 },
			{ 7, System.Windows.Forms.Keys.D7 },
			{ 8, System.Windows.Forms.Keys.D8 },
			{ 9, System.Windows.Forms.Keys.D9 }
		};
		private static readonly string cTextGroupsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			@"FJH\" + cThisAppName + @"\TextGroups");

		public static List<TextGroup> GetCurrentList()
		{
			List<TextGroup> tmplist = new List<TextGroup>();

			//Environment.CurrentDirectory = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
			if (!Directory.Exists(cTextGroupsDir))
			{
				Directory.CreateDirectory(cTextGroupsDir);
				//UserMessages.ShowWarningMessage("No text groups created yet.");
				return tmplist;
			}
			else
			{
				foreach (var dir in Directory.GetDirectories(cTextGroupsDir))
				{
					var groupName = Path.GetFileName(dir);
					tmplist.Add(new TextGroup(groupName));
					/*foreach (var numfile in Directory.GetFiles(dir))
					{
						int tmpint;
						if (int.TryParse(Path.GetFileNameWithoutExtension(numfile), out tmpint))
							if (keyBindings.Keys.Contains(tmpint))
							{
								if (TextGroupList.Count(tg => tg.GroupName.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)) == 0)
									TextGroupList.Add(new TextGroup(groupName, numfile));
							}
					}*/
				}
			}

			return tmplist;
		}

		public static TextGroup PromptForNewTextGroup()
		{
			string newGroupName = InputBoxWPF.Prompt("Enter the group name", "New Group");
			if (newGroupName == null) return null;
			else return new TextGroup(newGroupName);
		}

		public string GetNumberFilename(int number)
		{
			return Path.Combine(GroupDirPath, number + ".txt");
		}

		private void PopulateGroupTextList()
		{
			if (!Directory.Exists(GroupDirPath))
				Directory.CreateDirectory(GroupDirPath);
				//for (int i = 0; i <= 9; i++)
				//    this[i] = new TextItem("");

			foreach (var numfile in Directory.GetFiles(GroupDirPath))
			{
				int tmpint;
				if (int.TryParse(Path.GetFileNameWithoutExtension(numfile), out tmpint))
					if (cKeyBindings.Keys.Contains(tmpint))
					{
						try
						{
							this[tmpint] = new TextItem(File.ReadAllText(numfile));
						}
						catch (Exception exc)
						{
							UserMessages.ShowErrorMessage("Cannot read file details: " + exc.Message);
						}
						//if (TextGroupList.Count(tg => tg.GroupName.Equals(groupName, StringComparison.InvariantCultureIgnoreCase)) == 0)
						//    TextGroupList.Add(new TextGroup(groupName, numfile));
					}
			}
		}

		/*public TextGroup(string GroupName, IEnumerable<string> GroupTextList)
		{
			this.GroupTextList = new ObservableCollection<string>(GroupTextList);
			while (this.GroupTextList.Count > 10)
				this.GroupTextList.RemoveAt(this.GroupTextList.Count - 1);//This is dangerous but should never actually occur

			while (this.GroupTextList.Count < 10)
				this.GroupTextList.Add("");
		}*/

		public override string ToString()
		{
			return this.GroupName;//return base.ToString();
		}

		public class TextItem : INotifyPropertyChanged
		{
			private string _text;
			public string Text { get { return _text; } set { _text = value; OnPropertyChanged("Text"); } }

			public TextItem(string Text)
			{
				this.Text = Text;
			}

			public override string ToString()
			{
				return _text;
			}

			public event PropertyChangedEventHandler PropertyChanged = delegate { };
			public void OnPropertyChanged(params string[] propertyNames) { foreach (var pn in propertyNames) PropertyChanged(this, new PropertyChangedEventArgs(pn)); }
		}
	}

	public class Index1to0Converter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (!(value is int))
				return value;

			int val = (int)value;
			val++;//Add one so we start at 1 and not zero
			if (val > 10)
				return -998877;//This should not happen at this stage
			else if (val == 10)
				return 0;
			else
				return val;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

﻿using RainbowTaskbar.Editor.Pages;
using RainbowTaskbar.Editor.Pages.Edit;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace RainbowTaskbar.Editor {
    /// <summary>
    /// Interaction logic for EditorWindow.xaml
    /// </summary>
    public partial class EditorWindow : FluentWindow {

        public IContentDialogService contentDialogService;
        public EditorWindow() {

            SystemThemeWatcher.Watch(this);
            InitializeComponent();

            nav.Loaded += (_, _) => {
                nav.Navigate(typeof(Home));
                ApplicationThemeManager.ApplySystemTheme(true);
            };

            this.DataContext = App.editorViewModel;

            this.contentDialogService = new ContentDialogService();
            contentDialogService.SetContentPresenter(RootContentDialogPresenter);

            App.localization.Enable(Resources.MergedDictionaries);


            if (!App.monacoExtracted) {
                Stream stream = new MemoryStream(Properties.Resources.monaco);
                if (Directory.Exists(App.monacoDir))
                    Directory.Delete(App.monacoDir, true);
                ZipFile.ExtractToDirectory(stream, App.monacoDir);
            }
        }

        private void nav_Navigating(NavigationView sender, NavigatingCancelEventArgs args) {
            if (App.editorViewModel.EditPage is not null && App.editorViewModel.EditPage is InstructionEditPage) {
                var page = App.editorViewModel.EditPage as InstructionEditPage;
                page.Current.Stop();
                if (App.Settings.SelectedConfig is not null) App.Settings.SelectedConfig.Start();
            }
            if (App.editorViewModel.EditPage is not null && App.editorViewModel.EditPage is WebEditPage) {
                if (App.Settings.SelectedConfig is not null) App.Settings.SelectedConfig.Start();
            }

            if (App.editorViewModel.EditPage is not null && App.editorViewModel.EditPage.Modified) {

                args.Cancel = true;
                
                if(App.editorViewModel.EditPage is WebEditPage) {
                    var page = App.editorViewModel.EditPage as WebEditPage;
                    page.webView.Visibility = Visibility.Hidden;
                }

                var task = contentDialogService.ShowSimpleDialogAsync(
                    // todo: translations, instruction-config save or quit
                    new SimpleContentDialogCreateOptions() {
                        Title = "Save your work?",
                        Content = "Do you wish to save your current config?",
                        PrimaryButtonText = "Save",
                        SecondaryButtonText = "Don't Save",
                        CloseButtonText = "Cancel",
                });

                Task.Run(() => {
                    var res = task.Result;
                    var saving = false;

                    switch (res) {
                        case ContentDialogResult.Primary:
                            if (App.editorViewModel.EditPage is WebEditPage) {
                                var page = App.editorViewModel.EditPage as WebEditPage;

                                // INSANE!
                                Dispatcher.Invoke(() => {
                                    saving = true;
                                    page.Save();
                                    Task.Run(() => {
                                        Thread.Sleep(1500);
                                        saving = false;
                                        Dispatcher.Invoke(() => {
                                            try {
                                                page.webView.Dispose();
                                            }
                                            catch { }
                                        });
                                    });
                                });
                            }
                            goto case ContentDialogResult.Secondary;
                        case ContentDialogResult.Secondary:
                            Dispatcher.Invoke(() => {
                                // weird COM error sometimes?
                                try {
                                    if(!saving) (App.editorViewModel.EditPage as WebEditPage).webView.Dispose();
                                }
                                catch { }
                            });
                            App.editorViewModel.EditPage = null;
                            Dispatcher.Invoke(() => sender.Navigate(args.Page.GetType()));
                            Dispatcher.Invoke(() => { if (App.Settings.SelectedConfig is not null) App.Settings.SelectedConfig.Start(); });
                            break;
                        case ContentDialogResult.None:
                            if (App.editorViewModel.EditPage is WebEditPage) {
                                var page = App.editorViewModel.EditPage as WebEditPage;
                                Dispatcher.Invoke(() => page.webView.Visibility = Visibility.Visible);
                            }
                            break;
                    }
                });
                return;
                
            }
            if (App.editorViewModel.EditPage is not null && App.editorViewModel.EditPage is WebEditPage) {
                (App.editorViewModel.EditPage as WebEditPage).webView.Dispose();
            }
            if(App.editorViewModel.EditPage is not null) {
                App.editorViewModel.EditPage = null;
            }
        }

        private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = true;
            Hide();
        }
    }
}

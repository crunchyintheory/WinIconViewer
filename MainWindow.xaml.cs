// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.Drawing;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IconViewer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        MicaController m_backdropController;
        SystemBackdropConfiguration m_configurationSource;

        private AppWindow m_AppWindow;

        public MainWindow()
        {
            this.InitializeComponent();
            TrySetSystemBackdrop();

            m_AppWindow = GetAppWindowForCurrentWindow();
            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = m_AppWindow.TitleBar;
                // Hide default title bar.
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
            }
            else
            {
                // Title bar customization using these APIs is currently
                // supported only on Windows 11. In other cases, hide
                // the custom title bar element.
                AppTitleBar.Visibility = Visibility.Collapsed;
            }

            m_AppWindow.Resize(new Windows.Graphics.SizeInt32(1024, 768));

            if(App.Files?[0].IsOfType(StorageItemTypes.File) == true) {
                setFileDisplay(App.Files[0].As<IStorageFile>());
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        bool TrySetSystemBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Create the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_backdropController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();
    
                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            // so it doesn't try to use this closed window.
            if (m_backdropController != null)
            {
                m_backdropController.Dispose();
                m_backdropController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }

        private async void ContentFrame_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open";
        }

        private async void ContentFrame_Drop(object sender, DragEventArgs e)
        {
            if(e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if(items.Count > 0)
                {
                    var file = items[0] as StorageFile;
                    if(file.Name.EndsWith(".ico") || file.Name.EndsWith(".icns"))
                        setFileDisplay(file);
                }
            }

        }

        private async void setFileDisplay(IStorageFile file)
        {
            if(file.FileType == ".ico")
            {
                byte[] contents;
                using (var fileStream = await file.OpenStreamForReadAsync())
                {
                    contents = new byte[fileStream.Length];

                    fileStream.Read(contents);
                }

                ICOIconDir dir;

                dir.idReserved = BitConverter.ToUInt16(contents, 0);
                dir.idType = BitConverter.ToUInt16(contents, 2);
                dir.idCount = BitConverter.ToUInt16(contents, 4);

                dir.idEntries = new ICOIconDirEntry[dir.idCount];

                for(int i = 0; i < dir.idCount; i++)
                {
                    int offset = (i * 16) + 6;
                    dir.idEntries[i].bWidth = contents[offset];
                    dir.idEntries[i].bHeight = contents[offset + 1];
                    dir.idEntries[i].bColorCount = contents[offset + 2];
                    dir.idEntries[i].bReserved = contents[offset + 3];
                    dir.idEntries[i].wPlanes = BitConverter.ToUInt16(contents, offset + 4);
                    dir.idEntries[i].wBitCount = BitConverter.ToUInt16(contents, offset + 6);
                    dir.idEntries[i].dwBytesInRes = BitConverter.ToUInt32(contents, offset + 8);
                    dir.idEntries[i].dwImageOffset = BitConverter.ToUInt32(contents, offset + 12);

                    dir.idEntries[i].iconData = new byte[dir.idEntries[i].dwBytesInRes];
                    try
                    {
                        Array.Copy(contents, dir.idEntries[i].dwImageOffset, dir.idEntries[i].iconData, 0, dir.idEntries[i].dwBytesInRes);
                    } catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                Console.WriteLine(dir);

                try
                {
                    int i = 0;

                    uint PNGMagicBytes = 0x474e5089;
                    byte[] newIcon;

                    if (BitConverter.ToUInt32(dir.idEntries[i].iconData, 0) != PNGMagicBytes)
                    {
                        // Reconstruct BMP Header
                        byte[] headerDataTemp = new byte[14];
                        BitConverter.GetBytes((ushort)0x4D42).CopyTo(headerDataTemp, 0x00);
                        BitConverter.GetBytes((uint)dir.idEntries[i].dwBytesInRes + 14).CopyTo(headerDataTemp, 0x02);
                        BitConverter.GetBytes((uint)0x36).CopyTo(headerDataTemp, 0x0A);


                        newIcon = new byte[dir.idEntries[i].dwBytesInRes + 14];

                        Array.Copy(headerDataTemp, newIcon, 14);
                        Array.Copy(dir.idEntries[i].iconData, 0, newIcon, 14, dir.idEntries[i].iconData.Length);

                        newIcon[22] = newIcon[18];
                    }
                    else
                    {
                        newIcon = dir.idEntries[i].iconData;
                    }

                    MemoryStream stream = new MemoryStream(newIcon);

                    WriteableBitmap newImage = new WriteableBitmap(dir.idEntries[i].bWidth == 0 ? 256 : dir.idEntries[i].bWidth, dir.idEntries[i].bHeight == 0 ? 256 : dir.idEntries[i].bHeight);
                    await newImage.SetSourceAsync(stream.AsRandomAccessStream());

                    mainImage.Source = newImage;
                    mainImage.Height = newImage.PixelHeight;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
}

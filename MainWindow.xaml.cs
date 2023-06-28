using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Python.Runtime;


namespace UsbCamToMovieFile
{
	public partial class MainWindow : System.Windows.Window
	{
		/// <summary>
		/// video capture for usb camera
		/// </summary>
		VideoCapture m_capture = null;

		/// <summary>
		/// vidoe file path.
		/// </summary>
		string m_videoPath;

		/// <summary>
		/// audio file path.
		/// </summary>
		string m_audioPath;

		/// <summary>
		/// video writer
		/// </summary>
		VideoWriter m_writer = null;

		/// <summary>
		/// Video fps
		/// </summary>
		const int m_fps = 30;

		/// <summary>
		/// 録音用
		/// </summary>
		WaveInEvent m_waveIn;

		/// <summary>
		/// 録音用
		/// </summary>
		WaveFileWriter m_waveWriter;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// General exception proc.
		/// </summary>
		/// <param name="ex">exception</param>
		private void OnException(Exception ex)
		{
			_ = MessageBox.Show(this, $"{ex.Message}\n{ex.StackTrace}", "Error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		/// <summary>
		/// Close USB camera
		/// </summary>
		private void CloseCamera()
		{
			if (m_capture != null)
			{
				m_capture.Dispose();
				m_capture = null;
			}
		}

		/// <summary>
		/// Task: get camera image and display and save file.
		/// </summary>
		private void CameraTask()
		{
			var textPoint = new OpenCvSharp.Point(30, 30);
			//DateTime dtPrevFrame = DateTime.Now;

			while (true)
			{
				try
				{
					if (m_capture == null || m_capture.IsDisposed)
					{
						if (m_writer != null)
						{
							m_writer.Dispose();
							m_writer = null;
						}
						break;
					}

					Mat matCurFrame = new Mat();
					m_capture.Read(matCurFrame);

					matCurFrame.PutText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff K"), textPoint, HersheyFonts.HersheyComplexSmall, 1.0, Scalar.White);
					m_writer.Write(matCurFrame);

					_ = Dispatcher.BeginInvoke((Action)(() =>
					{
						if (m_capture != null)
						{
							imgCam.Source = BitmapSourceConverter.ToBitmapSource(matCurFrame);
						}
					}));
				}
				catch (Exception ex)
				{
					Dispatcher.Invoke(() => { OnException(ex); });
					break;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="defPath">Default path</param>
		/// <returns>Rturns file path. Returns null when canceled.</returns>
		public static string GetSaveFilePath(string defPath)
		{
			var dialog = new CommonSaveFileDialog();
			dialog.DefaultDirectory = System.IO.Path.GetDirectoryName(defPath);
			dialog.DefaultFileName = System.IO.Path.GetFileName(defPath);

			return CommonFileDialogResult.Ok ==  dialog.ShowDialog() ? dialog.FileName : null;
		}

		/// <summary>
		/// Window loaded handler.
		/// </summary>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				// pythonの設定
				var PYTHON_HOME = @"C:\Users\username\anaconda3";

				{
					// PATHの設定
					var envPaths = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
					string[] addPaths = {
						PYTHON_HOME,
						PYTHON_HOME + @"\Library\mingw-w64",
						PYTHON_HOME + @"\Library\usr\bin",
						PYTHON_HOME + @"\Library\bin",
						PYTHON_HOME + @"\Scripts",
					};
					string newPath = String.Join(System.IO.Path.PathSeparator.ToString(), addPaths) + System.IO.Path.PathSeparator.ToString() + envPaths;
					Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);

					// PYTHONPATHの設定
					//Environment.SetEnvironmentVariable("PYTHONPATH", PYTHON_HOME, EnvironmentVariableTarget.Process);

					// CONDA_** の設定（Anacondaのコマンドプロンプトを参考に）
					//Environment.SetEnvironmentVariable("CONDA_DEFAULT_ENV", "base", EnvironmentVariableTarget.Process);
					//Environment.SetEnvironmentVariable("CONDA_PREFIX", PYTHON_HOME, EnvironmentVariableTarget.Process);
					//Environment.SetEnvironmentVariable("CONDA_PROMPT_MODIFIER", "(base)", EnvironmentVariableTarget.Process);
					//Environment.SetEnvironmentVariable("CONDA_PYTHON_EXE", PYTHON_HOME + @"\python.exe", EnvironmentVariableTarget.Process);
					//Environment.SetEnvironmentVariable("CONDA_SHLVL", "1", EnvironmentVariableTarget.Process);
				}

				{
					// DLLの設定
					var PYTHON_DLL_PATH = PYTHON_HOME + @"\python310.dll";
					Runtime.PythonDLL = PYTHON_DLL_PATH;

					// HOMEの設定（python.exeの存在しているフォルダ）
					Environment.SetEnvironmentVariable("PYTHONHOME", PYTHON_HOME, EnvironmentVariableTarget.Process);
					PythonEngine.PythonHome = PYTHON_HOME; // Environment.GetEnvironmentVariable("PYTHONHOME", EnvironmentVariableTarget.Process);

					// 外部ライブラリの設定（必須だが、予めPythonEngine.PythonPathに値が入っている時は考慮が必要だと思う）
					string pythonPath = string.Join(System.IO.Path.PathSeparator.ToString(), new string[] {
						PythonEngine.PythonPath,
						PYTHON_HOME,
						System.IO.Path.Combine(PYTHON_HOME, @"DLLs"),
						System.IO.Path.Combine(PYTHON_HOME, @"Lib"),
						System.IO.Path.Combine(PYTHON_HOME, @"Lib\site-packages")
					}); ;
					PythonEngine.PythonPath = pythonPath;

					// 初期化
					PythonEngine.Initialize();

					// お試し
					{
						//using (Py.GIL())
						//{
						//	dynamic np = Py.Import("numpy");
						//	Console.WriteLine(np.sqrt(2));
						//}
					}

					// 別スレッドで実行できるように、明示的にGILを開放
					PythonEngine.BeginAllowThreads();
				}
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}

		/// <summary>
		/// Window closed handler.
		/// </summary>
		private void Window_Closed(object sender, EventArgs e)
		{
			try
			{
				CloseCamera();

				PythonEngine.Shutdown();
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}

		/// <summary>
		/// [Close] button clicked.
		/// </summary>
		private void btnClose_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Close();
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}

		/// <summary>
		/// [Open Cam] clicked.
		/// </summary>
		private void btnOpenCam_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// get filepath
				string defPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "movie1.mp4");
				m_videoPath = GetSaveFilePath(defPath);
				if (m_videoPath == null)
				{
					m_audioPath = null;
					return;
				}
				m_audioPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(m_videoPath), System.IO.Path.GetFileNameWithoutExtension(m_videoPath) + ".wav");

				Mouse.OverrideCursor = Cursors.Wait;

				// confirm camera
				try
				{
					CloseCamera();
					m_capture = VideoCapture.FromCamera(0);
					if (!m_capture.IsOpened())
						throw new Exception("Don't opened.");

					// 空打ち
					Mat matDummy = new Mat();
					_ = m_capture.Read(matDummy);

					var frameSize = new OpenCvSharp.Size(matDummy.Width, matDummy.Height);
					m_writer = new VideoWriter(m_videoPath, FourCC.H264, m_fps, frameSize);
				}
				catch (Exception)
				{
					_ = MessageBox.Show(this, "Cannot use USB Camera(0)", "Info", MessageBoxButton.OK, MessageBoxImage.Error);
				}

				// Audio initialize (NAudio)
				{
					m_waveIn = new WaveInEvent();
					m_waveIn.DeviceNumber = 0;
					m_waveIn.WaveFormat = new WaveFormat(48000, WaveIn.GetCapabilities(0).Channels);

					m_waveWriter = new WaveFileWriter(m_audioPath, m_waveIn.WaveFormat);
					m_waveIn.DataAvailable += (_, ee) =>
					{
						m_waveWriter?.Write(ee.Buffer, 0, ee.BytesRecorded);
						m_waveWriter?.Flush();
					};
					m_waveIn.RecordingStopped += (_, __) =>
					{
						m_waveWriter?.Flush();
					};

					m_waveIn.StartRecording();
				}

				// run task
				Task.Run(() => { CameraTask(); });

				// change button enabled
				btnOpenCam.IsEnabled = false;
				btnCloseCam.IsEnabled = true;
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}
		}

		/// <summary>
		/// [Close Cam] clicked.
		/// </summary>
		private void btnCloseCam_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				m_waveIn?.StopRecording();
				m_waveIn?.Dispose();
				m_waveIn = null;
				m_waveWriter?.Close();
				m_waveWriter = null;

				CloseCamera();

				btnOpenCam.IsEnabled = true;
				btnCloseCam.IsEnabled = false;

				// 映像ファイルと音声ファイルを結合する
				if (m_videoPath != null && m_audioPath != null &&
					System.IO.File.Exists(m_videoPath) && System.IO.File.Exists(m_audioPath))
				{
					_ = Task.Run(() =>
					{
						// Pythonの呼び出し（ロックが必要らしい）
						using (Py.GIL())
						{
							dynamic np = Py.Import("numpy");
							Console.WriteLine(np.sqrt(2));

							// moviepyのインポート
							dynamic mp = Py.Import("moviepy.editor");

							dynamic audio = mp.AudioFileClip(m_audioPath);
							dynamic video = mp.VideoFileClip(m_videoPath);
							video = video.set_audio(audio);
							video.write_videofile("movie_con.mp4");
						}
					});
				}
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}
	}
}

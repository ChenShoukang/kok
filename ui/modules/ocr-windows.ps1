param([Parameter(Mandatory=$true)][string]$InputFile,[Parameter(Mandatory=$true)][string]$OutputFile)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null=[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null=[Windows.Graphics.Imaging.BitmapDecoder,Windows.Foundation,ContentType=WindowsRuntime]
$null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$asTask=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
function Await-OcrTask($Operation,[Type]$ResultType){$task=$asTask.MakeGenericMethod($ResultType).Invoke($null,@($Operation));$task.GetAwaiter().GetResult()}
$file=Await-OcrTask ([Windows.Storage.StorageFile]::GetFileFromPathAsync([IO.Path]::GetFullPath($InputFile))) ([Windows.Storage.StorageFile])
$stream=Await-OcrTask ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
try {
 $decoder=Await-OcrTask ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
 $bitmap=Await-OcrTask ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,[Windows.Graphics.Imaging.BitmapAlphaMode]::Ignore)) ([Windows.Graphics.Imaging.SoftwareBitmap])
 try {
  $engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
  if(-not $engine){$available=[Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages;if($available.Count -gt 0){$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($available[0])}}
  if(-not $engine){throw 'No Windows OCR language is installed. Add an OCR language in Windows settings.'}
  $result=Await-OcrTask ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
  $data=@{text=$result.Text;language=$engine.RecognizerLanguage.LanguageTag;engine='Windows.Media.Ocr';width=$bitmap.PixelWidth;height=$bitmap.PixelHeight}
  [IO.File]::WriteAllText($OutputFile,($data|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
 } finally {$bitmap.Dispose()}
} finally {$stream.Dispose()}

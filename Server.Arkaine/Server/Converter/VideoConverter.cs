using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Diagnostics;
using System.Drawing;

namespace Server.Arkaine.Converter
{
    public class VideoConverter
    {
        public async Task ExtractThumbnail()
        {
            // -ss 00:00:01.000 -i C:\\temp\\TEST.MP4 -vf 'scale=300:300:force_original_aspect_ratio=decrease' -vframes 1 C:\\temp\\zz.jpg

            var stream = File.OpenRead("C:\\temp\\dolly.MP4");

            //var output = File.Create("C:\\temp\\zz.MP4");

            //await FFMpegArguments
            //    .FromPipeInput(new StreamPipeSource(stream))
            //    .OutputToPipe(new StreamPipeSink(output), options => options
            //        .WithVideoCodec("vp9")
            //        .ForceFormat("webm"))
            //    .ProcessAsynchronously();

            FFMpegArguments
                .FromPipeInput(new StreamPipeSource(stream), options => options.ForceFormat("rawvideo"))
                .OutputToFile("C:\\temp\\zz.png", true, options => options
                    .ForceFormat("rawvideo")
                    .WithVideoCodec(VideoCodec.Png)
                    .WithFrameOutputCount(1))
                .ProcessSynchronously();
        }
    }
}

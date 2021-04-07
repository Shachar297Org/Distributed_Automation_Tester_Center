using Console.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TestCenter.ViewModels;
using TestCenterConsole.Models;

namespace TestCenter.Hubs
{
    public class TestCenterHub : Hub
    {
        public ChannelReader<AwsMetricsData> TestCenterData(
            int count,
            int delay,
            CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<AwsMetricsData>();
            _ = WriteItemsAsync(channel.Writer, count, delay, cancellationToken);
            return channel.Reader;
        }

        private async Task WriteItemsAsync(
            ChannelWriter<AwsMetricsData> writer,
            int count,
            int delay,
            CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteAsync(new AwsMetricsData() { EventsInRDS = i });
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
            }

            writer.TryComplete();
        }
    }
}

// The MIT License (MIT)
// 
// Copyright (c) 2014 James White of Gbase.com
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ISchemm.NLog.JsonTarget
{
    internal sealed class JsonPoster : IDisposable
    {
        public int ActivePosts = 0;
        private HttpClient _httpClient;
        private HashSet<CancellationTokenSource> _runningDelays;

        public JsonPoster()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _runningDelays = new HashSet<CancellationTokenSource>();
        }

        public void CancelAll() {
            CancellationTokenSource[] array = _runningDelays.ToArray();
            foreach (var tokenSource in array) {
                tokenSource.Cancel();
            }
            _runningDelays.Clear();
        }

        public void Dispose()
        {
            if (_httpClient == null)
                return;
          
            Debug.WriteLine("Disposing..");

            _httpClient.Dispose();
            _httpClient = null;
        }

        public JsonPoster AddHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Add(name, value);
            return this;
        }

        public async void Post(Uri uri, string json, int[] retries)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            _runningDelays.Add(tokenSource);

            try
            {
                var posts = Interlocked.Increment(ref ActivePosts);

#if DEBUG
                Debug.WriteLine("JsonPoster posting ({0})...", posts);
#endif

                for (int i = 0; i < retries.Length; i++)
                {
                    try
                    {
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _httpClient.PostAsync(uri, content, tokenSource.Token);//.WithTimeout(_httpClient.Timeout);

                        response.EnsureSuccessStatusCode();
                        break;
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Debug.WriteLine($"JsonPoster {json.GetHashCode()}: waiting for {TimeSpan.FromSeconds(retries[i])}, attempt {i + 1}/{retries.Length}: {ex.GetType().Name}: {ex.Message}");
#endif
                        await Task.Delay(retries[i] * 1000, tokenSource.Token);
                    }
                }
            }
            catch (TaskCanceledException cancelledEx)
            {
#if DEBUG
                Debug.WriteLine("Task #{0} cancelled..", cancelledEx.Task.Id);
#endif
            }
            finally
            {
                _runningDelays.Remove(tokenSource);
                var posts = Interlocked.Decrement(ref ActivePosts);

                Debug.WriteLine("JsonPoster completed ({0})...", posts);
            }
        }
    }

    public static class TaskExtensions
    {
        public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            var delay = task.ContinueWith(t => t.Result
                , new CancellationTokenSource(timeout).Token);
            return Task.WhenAny(task, delay).Unwrap();
        }
    }
}
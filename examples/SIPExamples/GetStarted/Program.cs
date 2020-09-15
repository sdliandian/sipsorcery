﻿//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: A getting started program to demonstrate how to use the SIPSorcery
// library to place a call.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 13 Oct 2019	Aaron Clauson	Created, Dublin, Ireland.
// 31 Dec 2019  Aaron Clauson   Changed from an OPTIONS example to a call example.
// 20 Feb 2020  Aaron Clauson   Switched to RtpAVSession and simplified.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Windows;

namespace demo
{
    class Program
    {
        private static string DESTINATION = "time@sipsorcery.com";
        //private static string DESTINATION = "sip:pcdodo@192.168.0.50";
        private static SIPEndPoint OUTBOUND_PROXY = null; // SIPEndPoint.ParseSIPEndPoint("udp:192.168.0.148:5060");

        static async Task Main()
        {
            Console.WriteLine("SIPSorcery Getting Started Demo");

            AddConsoleLogger();
            CancellationTokenSource exitCts = new CancellationTokenSource();

            var sipTransport = new SIPTransport();

            EnableTraceLogs(sipTransport);

            var userAgent = new SIPUserAgent(sipTransport, OUTBOUND_PROXY);
            userAgent.ClientCallFailed += (uac, error, sipResponse) => Console.WriteLine($"Call failed {error}.");
            userAgent.OnCallHungup += (dialog) => exitCts.Cancel();

            var windowsAudio = new WindowsAudioEndPoint(new AudioEncoder());
            var voipMediaSession = new VoIPMediaSession(windowsAudio.ToMediaEndPoints());
            voipMediaSession.AcceptRtpFromAny = true;
            
            // Place the call and wait for the result.
            var callTask = userAgent.Call(DESTINATION, null, null, voipMediaSession);

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;

                if (userAgent != null)
                {
                    if (userAgent.IsCalling || userAgent.IsRinging)
                    {
                        Console.WriteLine("Cancelling in progress call.");
                        userAgent.Cancel();
                    }
                    else if (userAgent.IsCallActive)
                    {
                        Console.WriteLine("Hanging up established call.");
                        userAgent.Hangup();
                    }
                };

                exitCts.Cancel();
            };

            Console.WriteLine("press ctrl-c to exit...");

            bool callResult = await callTask;

            if (callResult)
            {
                Console.WriteLine($"Call to {DESTINATION} succeeded.");
                exitCts.Token.WaitHandle.WaitOne();
            }
            else
            {
                Console.WriteLine($"Call to {DESTINATION} failed.");
            }

            Console.WriteLine("Exiting...");

            if(userAgent?.IsHangingUp == true)
            {
                Console.WriteLine("Waiting 1s for the call hangup or cancel to complete...");
                await Task.Delay(1000);
            }

            // Clean up.
            sipTransport.Shutdown();
        }

        /// <summary>
        /// Enable detailed SIP log messages.
        /// </summary>
        private static void EnableTraceLogs(SIPTransport sipTransport)
        {
            sipTransport.SIPRequestInTraceEvent += (localEP, remoteEP, req) =>
            {
                Console.WriteLine($"Request received: {localEP}<-{remoteEP}");
                Console.WriteLine(req.ToString());
            };

            sipTransport.SIPRequestOutTraceEvent += (localEP, remoteEP, req) =>
            {
                Console.WriteLine($"Request sent: {localEP}->{remoteEP}");
                Console.WriteLine(req.ToString());
            };

            sipTransport.SIPResponseInTraceEvent += (localEP, remoteEP, resp) =>
            {
                Console.WriteLine($"Response received: {localEP}<-{remoteEP}");
                Console.WriteLine(resp.ToString());
            };

            sipTransport.SIPResponseOutTraceEvent += (localEP, remoteEP, resp) =>
            {
                Console.WriteLine($"Response sent: {localEP}->{remoteEP}");
                Console.WriteLine(resp.ToString());
            };

            sipTransport.SIPRequestRetransmitTraceEvent += (tx, req, count) =>
            {
                Console.WriteLine($"Request retransmit {count} for request {req.StatusLine}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };

            sipTransport.SIPResponseRetransmitTraceEvent += (tx, resp, count) =>
            {
                Console.WriteLine($"Response retransmit {count} for response {resp.ShortDescription}, initial transmit {DateTime.Now.Subtract(tx.InitialTransmit).TotalSeconds.ToString("0.###")}s ago.");
            };
        }

        /// <summary>
        ///  Adds a console logger. Can be omitted if internal SIPSorcery debug and warning messages are not required.
        /// </summary>
        private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
            return factory.CreateLogger<Program>();
        }
    }
}

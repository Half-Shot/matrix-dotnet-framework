using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatrixSDK.Client;
using MatrixSDK.Structures;
namespace MatrixBotFramework
{
	public class StatusOptions{
		public StatusOptions(string ItemName,string ProductName,string Version, string Room, int IntervalMinutes, bool StartAutomatically){
			this.ItemName = ItemName;
			this.ProductName = ProductName;
			this.Version = Version;
			this.Room = Room;
			this.IntervalMinutes = IntervalMinutes;
			this.StartAutomatically = StartAutomatically;
		}
		public readonly string ItemName;
		public readonly string ProductName;
		public readonly string Version;
        public readonly string Room = "";
		public readonly int IntervalMinutes = 30;
		public readonly bool StartAutomatically;
	}

    public class MatrixBot<T>
    {
        public MatrixClient Client;
        public Dictionary<BotCmd,MethodInfo> Cmds = new Dictionary<BotCmd, MethodInfo>();
        public MethodInfo fallback = null;
        private string prefix;
        public bool AutoAcceptInvite = false;

		StatusOptions statusOpts;
		System.Threading.Timer status_timer;

        public MatrixBot (
			string cmdprefix,
			string host,
			string user,
			string pass,
			string[] rooms,
			bool AutoAcceptInvite = false,
			StatusOptions statusOptions = null
		)
		{
			prefix = cmdprefix;
			Client = new MatrixClient (host);
			Client.AddStateEventType ("uk.half-shot.status", typeof(HSStatusEvent));

			this.AutoAcceptInvite = AutoAcceptInvite;

			Client.OnInvite += (roomid, joined) => {
				if (AutoAcceptInvite) {
					MatrixRoom room = Client.JoinRoom (roomid);
					if (room != null) {
						room.OnMessage += Room_OnMessage;
					}

				}
			};

			Client.LoginWithPassword (user, pass);

			MatrixRoom[] existingRooms = Client.GetAllRooms ();
			foreach (string roomid in rooms) {
				MatrixRoom room = roomid.StartsWith ("#") ? Client.GetRoomByAlias (roomid) : Client.GetRoom (roomid);
				if (existingRooms.Contains (room)) {
					continue;
				}
				if (room == null) {
					room = Client.JoinRoom (roomid);
					if (room != null) {
						Console.WriteLine ("\tJoined " + roomid);
					} else {
						Console.WriteLine ("\tCouldn't find " + roomid);
					}
				}
			}

			existingRooms = Client.GetAllRooms ();
			foreach (MatrixRoom room in existingRooms) {
				room.OnMessage += Room_OnMessage;
			}


			//Find commands
			foreach (MethodInfo method in typeof(T).GetMethods(BindingFlags.Static|BindingFlags.Public)) {
				BotCmd cmd = method.GetCustomAttribute<BotCmd> ();
				if (cmd != null) {
					Cmds.Add (cmd, method);
				}
				if (method.GetCustomAttribute<BotFallback> () != null) {
					if (fallback != null) {
						Console.WriteLine ("WARN: You have more than one fallback command set, overwriting previous");
					}
					fallback = method;
				}
			}

			if (statusOptions != null && statusOptions.StartAutomatically) {
				//Start Status Thread.
				statusOpts = statusOptions;
				StartStatusReporting(statusOptions.IntervalMinutes);

			}
        }

        private void ReportStatus (object state)
		{	
			MatrixRoom room = Client.GetRoom (statusOpts.Room);
			if (room == null) {
				room = Client.JoinRoom (statusOpts.Room);
			}
			if (room == null) {
				//Couldn't update status.
				//TODO: Fail somehow.
				StopStatusReporting();
			}
			HSStatusEvent evt = new HSStatusEvent(){
				name = statusOpts.ProductName,
				status = HSStatusEvent.STATUS_UP,
				code = "OK",
				message = "Bot Operational",
				version = statusOpts.Version,
				timestamp = (long)((DateTime.Now - new DateTime(1970,01,01)).TotalMilliseconds)
			};
			room.SendState(evt, "uk.half-shot.status", statusOpts.ItemName);
		}

        public void StartStatusReporting (int MinuteInterval)
		{
			if (status_timer == null) {
				status_timer = new System.Threading.Timer(
					ReportStatus,
					null,
					1,
					MinuteInterval*60000
				);
			}
		}

		public void StopStatusReporting ()
		{
			if (status_timer != null) {
				status_timer.Dispose ();
			}
		}

        public void NotifyAll(string text){
            foreach(MatrixRoom room in Client.GetAllRooms()){
                room.SendNotice(text);
            }
        }

        public static void GenerateHelpText(Type staticCommandClass, out string html, out string plain){
            string helptext = "";
            foreach(MethodInfo method in staticCommandClass.GetMethods(BindingFlags.Static|BindingFlags.Public)){
                BotCmd c = method.GetCustomAttribute<BotCmd> ();
                BotHelp h= method.GetCustomAttribute<BotHelp> ();

                if (c != null && h != null) {
                    helptext += String.Format("<p><strong>{0}</strong> {1}</p>",c.CMD, h != null ? System.Web.HttpUtility.HtmlEncode(h.HelpText) : "");
                }
            }
            plain = helptext.Replace("<strong>","").Replace("</strong>","").Replace("<p>","").Replace("</p>","\n");
            html = helptext;
        }

        void Room_OnMessage (MatrixRoom room, MatrixSDK.Structures.MatrixEvent evt)
        {
            if (evt.age > 3000) {
                return; // Too old
            }

            string msg = ((MatrixMRoomMessage)evt.content).body;

            if (msg.ToUpper().StartsWith (prefix.ToUpper())) {
                msg = msg.Substring (prefix.Length+1);
                string[] parts = msg.Split (' ');
                string cmd = parts [0].ToLower ();
                try
                {
                    MethodInfo method = Cmds.First(x => { 
                        return (x.Key.CMD == cmd) || ( x.Key.BeginsWith.Any( y => cmd.StartsWith(y) ));
                    }).Value; 

                    Task task = new Task (() => {
                        method.Invoke (null, new object[3]{ msg, evt.sender, room });
                    });
                    task.Start ();  
                }
                catch(InvalidOperationException){
                    Task task = new Task (() => {
                        fallback.Invoke (null, new object[3]{ msg, evt.sender, room });
                    });
                    task.Start ();  
                }
                catch(Exception e){
                    Console.Error.WriteLine ("Problem with one of the commands");
                    Console.Error.WriteLine (e);
                }
            }
        }
    }
}


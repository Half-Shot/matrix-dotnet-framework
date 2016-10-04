using System;
using MatrixSDK.Structures;
namespace MatrixBotFramework
{
	public class HSStatusEvent : MatrixRoomStateEvent
	{
		public string name;
		public string status;
		public string code;
		public string message;
		public string version;
		public long timestamp;
		public const string STATUS_UP = "up";
		public const string STATUS_DOWN = "down";
	}
}


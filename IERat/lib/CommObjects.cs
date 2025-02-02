﻿using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace IERat.lib
{
    public class ResponseObject
    {
        public string Type { get; set; }
        public Guid AgentID { get; set; }
        //public TaskObject Task { get; set; }
        public List<TaskObject> Tasks { get; set; }
        public string Notes { get; set; }

    }
    public class RequestObject
    {
        public RequestObject(Guid ID)
        {
            AgentID = ID;
            CompletedTasks = new List<TaskObject>();
        }
        public Guid AgentID { get; set; }
        public List<TaskObject> CompletedTasks { get; set; }
        public string ToJSON()
        {
            var js = new JavaScriptSerializer { MaxJsonLength = 2097152 * 3 };
            return js.Serialize(this);
        }
    }
}

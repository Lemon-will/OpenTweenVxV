﻿// OpenTween - Client of Twitter
// Copyright (c) 2014 kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
//
// This file is part of OpenTween.
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OpenTween.Api.DataModel
{
    [DataContract]
    public class TwitterList
    {
        [DataMember(Name = "user")]
        public TwitterUser User { get; set; }

        [DataMember(Name = "following")]
        public bool Following { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "full_name")]
        public string FullName { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "id_str")]
        public string IdStr { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "uri")]
        public string Uri { get; set; }

        [DataMember(Name = "subscriber_count")]
        public int SubscriberCount { get; set; }

        [DataMember(Name = "member_count")]
        public int MemberCount { get; set; }

        [DataMember(Name = "mode")]
        public string Mode { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        /// <exception cref="SerializationException"/>
        public static TwitterList ParseJson(string json)
            => MyCommon.CreateDataFromJson<TwitterList>(json);

        /// <exception cref="SerializationException"/>
        public static TwitterList[] ParseJsonArray(string json)
            => MyCommon.CreateDataFromJson<TwitterList[]>(json);
    }
}

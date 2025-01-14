﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using System.Web;
using Microsoft.IdentityModel.Tokens;
using OpenTokSDK.Util;
using OpenTokSDK.Exception;
using Vonage.Request;
using Vonage.Video.Authentication;

namespace OpenTokSDK
{
    /// <summary>
    /// Defines values for the mediaMode parameter of the <see cref="OpenTok.CreateSession"/> method of the 
    /// <see cref="OpenTok"/> class.
    /// </summary>
    public enum MediaMode
    {
        /// <summary>
        /// The session will transmit streams using the OpenTok Media Router.
        /// </summary>
        ROUTED,
        /// <summary>
        /// The session will attempt to transmit streams directly between clients. If two clients
        /// cannot send and receive each others' streams, due to firewalls on the clients' networks,
        /// their streams will be relayed using the OpenTok TURN Server.
        /// </summary>
        RELAYED
    }

    /// <summary>
    /// Defines values for the archiveMode property of the <see cref="Session"/> object.
    /// You also use these values for the archiveMode parameter of the <see cref="OpenTok.CreateSession"/> method.
    /// </summary>
    public enum ArchiveMode
    {
        /// <summary>
        /// The session is not archived automatically. To archive the session, you can call the
        /// <see cref="OpenTok.StartArchive"/> method.
        /// </summary>
        MANUAL,
        /// <summary>
        /// The session is archived automatically (as soon as there are clients publishing streams
        /// to the session).
        /// </summary>
        ALWAYS
    }

    /// <summary>
    /// Represents an OpenTok session. Use the <see cref="OpenTok.CreateSession"/> method of the
    /// <see cref="OpenTok"/> class to create an OpenTok session. Use the Id property of the
    /// <see cref="Session"/> object to get the session ID.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// The session ID, which uniquely identifies the session.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Your OpenTok API key.
        /// </summary>
        public int ApiKey { get; private set; }

        /// <summary>
        /// Your OpenTok API secret.
        /// </summary>
        public string ApiSecret { get; private set; }

        /// <summary>
        /// The location hint IP address.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Defines whether the session will transmit streams using the OpenTok Media Router
        /// (<see cref="MediaMode.ROUTED"/>) or attempt to transmit streams directly between clients
        /// (<see cref="MediaMode.RELAYED"/>).
        /// </summary>
        public MediaMode MediaMode { get; private set; }

        /// <summary>
        /// Defines whether the session is automatically archived (<see cref="ArchiveMode.ALWAYS"/>)
        /// or not (<see cref="ArchiveMode.MANUAL"/>).
        /// </summary>
        public ArchiveMode ArchiveMode { get; private set; }

        private const int MAX_CONNECTION_DATA_LENGTH = 1000;

        internal Session(string sessionId, int apiKey, string apiSecret)
        {
            this.Id = sessionId;
            this.ApiKey = apiKey;
            this.ApiSecret = apiSecret;
        }

        internal static Session FromShim(string sessionId, string applicationId, string privateKey, string location, MediaMode mediaMode, ArchiveMode archiveMode)
        {
            return new Session()
            {
                Id = sessionId,
                ArchiveMode = archiveMode,
                MediaMode = mediaMode,
                Location = location,
                ApplicationId = applicationId,
                PrivateKey = privateKey,
            };
        }
        
        internal static Session FromLegacy(string sessionId,  int apiKey, string apiSecret, string location, MediaMode mediaMode, ArchiveMode archiveMode)
        {
            return new Session()
            {
                Id = sessionId,
                ArchiveMode = archiveMode,
                MediaMode = mediaMode,
                Location = location,
                ApiKey = apiKey,
                ApiSecret = apiSecret,
            };
        }

        private string ApplicationId { get; set; }
        private string PrivateKey { get; set; }

        private Session()
        {
        }

        /// <summary>
        /// Creates a token for connecting to an OpenTok session. In order to authenticate a user
        /// connecting to an OpenTok session that user must pass an authentication token along with
        /// the API key.
        /// </summary>
        /// <param name="role">
        /// The role for the token. Valid values are defined in the Role enum:
        /// - <see cref="Role.SUBSCRIBER"/> A subscriber can only subscribe to streams.
        /// - <see cref="Role.PUBLISHER"/> A publisher can publish streams, subscribe to
        ///   streams, and signal. (This is the default value if you do not specify a role.)
        /// - <see cref="Role.MODERATOR"/> In addition to the privileges granted to a
        ///   publisher, in clients using the OpenTok.js library, a moderator can call the
        ///   forceUnpublish() and forceDisconnect() method of the Session object.
        /// </param>
        /// <param name="expireTime">
        /// The expiration time of the token, in seconds since the UNIX epoch.
        /// Pass in 0 to use the default expiration time of 24 hours after the token creation time.
        /// The maximum expiration time is 30 days after the creation time.
        /// </param>
        /// <param name="data">
        /// A string containing connection metadata describing the end-user. For example,
        /// you can pass the user ID, name, or other data describing the end-user. The length of the
        /// string is limited to 1000 characters. This data cannot be updated once it is set.
        /// </param>
        /// <param name="initialLayoutClassList"></param>
        /// <returns>The token string.</returns>
        public string GenerateT1Token(Role role = Role.PUBLISHER, double expireTime = 0, string data = null, List<string> initialLayoutClassList = null)
        {
            double createTime = OpenTokUtils.GetCurrentUnixTimeStamp();
            int nonce = OpenTokUtils.GetRandomNumber();

            string dataString = BuildDataString(role, expireTime, data, createTime, nonce, initialLayoutClassList);
            return BuildTokenString(dataString);
        }
        
        /// <summary>
        /// Creates a token for connecting to an OpenTok session. In order to authenticate a user
        /// connecting to an OpenTok session that user must pass an authentication token along with
        /// the API key.
        /// </summary>
        /// <param name="role">
        /// The role for the token. Valid values are defined in the Role enum:
        /// - <see cref="Role.SUBSCRIBER"/> A subscriber can only subscribe to streams.
        /// - <see cref="Role.PUBLISHER"/> A publisher can publish streams, subscribe to
        ///   streams, and signal. (This is the default value if you do not specify a role.)
        /// - <see cref="Role.MODERATOR"/> In addition to the privileges granted to a
        ///   publisher, in clients using the OpenTok.js library, a moderator can call the
        ///   forceUnpublish() and forceDisconnect() method of the Session object.
        /// </param>
        /// <param name="expireTime">
        /// The expiration time of the token, in seconds since the UNIX epoch.
        /// Pass in 0 to use the default expiration time of 24 hours after the token creation time.
        /// The maximum expiration time is 30 days after the creation time.
        /// </param>
        /// <param name="data">
        /// A string containing connection metadata describing the end-user. For example,
        /// you can pass the user ID, name, or other data describing the end-user. The length of the
        /// string is limited to 1000 characters. This data cannot be updated once it is set.
        /// </param>
        /// <param name="initialLayoutClassList"></param>
        /// <returns>The token string.</returns>
        public string GenerateToken(Role role = Role.PUBLISHER, double expireTime = 0, string data = null, List<string> initialLayoutClassList = null) =>
            !string.IsNullOrEmpty(this.ApplicationId)
        ? new TokenGenerator().GenerateSessionToken(this.ApplicationId, this.PrivateKey, this.Id)
        : new TokenGenerator().GenerateSessionToken(new TokenData()
            {
                ApiSecret = this.ApiSecret,
                Role = role,
                ApiKey = this.ApiKey.ToString(),
                Data = data,
                SessionId = this.Id,
                ExpireTime = expireTime,
                InitialLayoutClasses = initialLayoutClassList ?? Enumerable.Empty<string>(),
            });

        private string BuildTokenString(string dataString)
        {
            string signature = OpenTokUtils.EncodeHMAC(dataString, this.ApiSecret);

            StringBuilder innerBuilder = new StringBuilder();
            innerBuilder.Append(string.Format("partner_id={0}", this.ApiKey));
            innerBuilder.Append(string.Format("&sig={0}:{1}", signature, dataString));

            byte[] innerBuilderBytes = Encoding.UTF8.GetBytes(innerBuilder.ToString());
            return "T1==" + Convert.ToBase64String(innerBuilderBytes);
        }

        private string BuildDataString(Role role, double expireTime, string connectionData, double createTime, int nonce, List<string> initialLayoutClassList)
        {
            StringBuilder dataStringBuilder = new StringBuilder();

            dataStringBuilder.Append(string.Format("session_id={0}", this.Id));
            dataStringBuilder.Append(string.Format("&create_time={0}", (long)createTime));
            dataStringBuilder.Append(string.Format("&nonce={0}", nonce));
            dataStringBuilder.Append(string.Format("&role={0}", role.ToString().ToLowerInvariant()));

            if (initialLayoutClassList != null)
            {
                dataStringBuilder.Append(string.Format("&initial_layout_class_list={0}", String.Join(" ", initialLayoutClassList)));
            }

            if (CheckExpireTime(expireTime, createTime))
            {
                dataStringBuilder.Append(string.Format("&expire_time={0}", (long)expireTime));
            }

            if (CheckConnectionData(connectionData))
            {
                dataStringBuilder.Append(string.Format("&connection_data={0}", HttpUtility.UrlEncode(connectionData)));
            }

            return dataStringBuilder.ToString();
        }

        private bool CheckExpireTime(double expireTime, double createTime)
        {
            if (expireTime == 0)
            {
                return false;
            }
            else if (expireTime > createTime && expireTime <= OpenTokUtils.GetCurrentUnixTimeStamp() + 2592000)
            {
                return true;
            }
            else
            {
                throw new OpenTokArgumentException("Invalid expiration time for token " + expireTime + ". Expiration time " +
                                                        " has to be positive and less than 30 days");
            }
        }

        private bool CheckConnectionData(string connectionData)
        {
            if (String.IsNullOrEmpty(connectionData))
            {
                return false;
            }
            else if (connectionData.Length <= MAX_CONNECTION_DATA_LENGTH)
            {
                return true;
            }
            else
            {
                throw new OpenTokArgumentException("Invalid connection data, it cannot be longer than 1000 characters");
            }
        }
    }
}

﻿/*
 * Licensed to Apereo under one or more contributor license
 * agreements. See the NOTICE file distributed with this work
 * for additional information regarding copyright ownership.
 * Apereo licenses this file to you under the Apache License,
 * Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a
 * copy of the License at:
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.Web;
using DotNetCasClient.Security;
using DotNetCasClient.Utils;
using DotNetCasClient.Validation.Schema.Cas30;

namespace DotNetCasClient.Validation.TicketValidator
{
    /// <summary>
    /// CAS 3.0 Ticket Validator
    /// </summary>
    /// <remarks>
    /// This is an extension of the CAS 2.0 ticket validator that supports the v3.0 endpoints
	/// and extra CAS attributes that may be in the response.
    /// </remarks>
    /// <author>Scott Battaglia</author>
    /// <author>Catherine D. Winfrey (.Net)</author>
    /// <author>William G. Thompson, Jr. (.Net)</author>
    /// <author>Marvin S. Addison</author>
    /// <author>Scott Holodak (.Net)</author>
	/// <author>Blair Allen</author>
    class Cas30ServiceTicketValidator : AbstractCasProtocolTicketValidator
    {
        #region Properties
        /// <summary>
        /// The endpoint of the validation URL.  Should be relative (i.e. not start with a "/").
        /// i.e. p3/proxyValidate or p3/serviceValidate.
        /// </summary>
        public override string UrlSuffix
        {
            get
            {
                if (CasAuthentication.ProxyTicketManager != null)
                {
                    return "p3/proxyValidate";
                }
                else
                {
                    return "p3/serviceValidate";
                }
            }
        }
		#endregion

		#region Methods 
		/// <summary>
		/// Performs Cas30ServiceTicketValidator initialization.
		/// </summary>
		public override void Initialize()
		{
			if (CasAuthentication.ProxyTicketManager != null)
			{
				CustomParameters.Add("pgtUrl", HttpUtility.UrlEncode(UrlUtil.ConstructProxyCallbackUrl()));
			}
		}

		/// <summary>
		/// Parses the response from the server into a CAS Assertion and includes this in
		/// a CASPrincipal.
		/// </summary>
		/// <param name="response">the response from the server, in any format.</param>
		/// <param name="ticket">The ticket used to generate the validation response</param>
		/// <returns>
		/// a Principal backed by a CAS Assertion, if one could be created from the response.
		/// </returns>
		/// <exception cref="TicketValidationException">
		/// Thrown if creation of the Assertion fails.
		/// </exception>
		protected override ICasPrincipal ParseResponseFromServer(string response, string ticket)
        {
            if (String.IsNullOrEmpty(response))
            {
                throw new TicketValidationException("CAS Server response was empty.");
            }

            ServiceResponse serviceResponse;
            try
            {
                // Attempt to deserialize the response XML into an instance of one of the response types
                serviceResponse = ServiceResponse.ParseResponse(response);
            }
            catch (InvalidOperationException)
            {
                throw new TicketValidationException("CAS Server response does not conform to CAS 3.0 schema");
            }
            
            if (serviceResponse.IsAuthenticationSuccess)
            {
                // The response indicates that validation of the service ticket was successful, so
                // pull out properties from the response in order to build a CAS principal
                AuthenticationSuccess authSuccessResponse = (AuthenticationSuccess)serviceResponse.Item;

                if (String.IsNullOrEmpty(authSuccessResponse.User))
                {
                    throw new TicketValidationException(string.Format("CAS Server response parse failure: missing 'cas:user' element."));
                }

                string proxyGrantingTicketIou = authSuccessResponse.ProxyGrantingTicket;

                if (CasAuthentication.ProxyTicketManager != null && !string.IsNullOrEmpty(proxyGrantingTicketIou))
                {
                    // Since a proxy ticket manager and an IOU for a proxy-granting ticket both
                    // exist, add the mapping to the ticket in the proxy ticket manager
                    string proxyGrantingTicket = CasAuthentication.ProxyTicketManager.GetProxyGrantingTicket(proxyGrantingTicketIou);
                    if ( proxyGrantingTicket != null )
                        CasAuthentication.ProxyTicketManager.InsertProxyGrantingTicketMapping( proxyGrantingTicketIou, proxyGrantingTicket );
                }

                if (authSuccessResponse.Proxies != null && authSuccessResponse.Proxies.Length > 0)
                {
                    // Return a new CAS principal that contains the proxy information
                    return new CasPrincipal(new Assertion(authSuccessResponse.User), proxyGrantingTicketIou, authSuccessResponse.Proxies);
                } 
                else
                {
                    // Return a new CAS principal that contains the user identity and any extra CAS attributes
                    return new CasPrincipal(new Assertion(authSuccessResponse.User, authSuccessResponse.Attributes), proxyGrantingTicketIou);
                }
            }
            
            if (serviceResponse.IsAuthenticationFailure)
            {
                // The response indicates that validation of the service ticket was not successful,
                // so throw an exception containing details about the failure
                AuthenticationFailure authFailureResponse;
                try
                {
                    authFailureResponse = (AuthenticationFailure) serviceResponse.Item;
                }
                catch
                {
                    // The item stored in the service response could not be cast as an
                    // AuthenticationFailure object, so just throw a generic exception
                    throw new TicketValidationException("CAS ticket could not be validated.");
                }

                // Throw a ticket validation exception with specific details about the validation failure
                throw new TicketValidationException(authFailureResponse.Message, authFailureResponse.Code);
            }
            
            if (serviceResponse.IsProxySuccess)
            {
                throw new TicketValidationException("Unexpected service validate response: ProxySuccess");
            }

            if (serviceResponse.IsProxyFailure)
            {
                throw new TicketValidationException("Unexpected service validate response: ProxyFailure");
            }

            throw new TicketValidationException("Failed to validate CAS ticket.");
        }
        #endregion
    }
}

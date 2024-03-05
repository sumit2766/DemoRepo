using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace LeadOpportunityPlugins
{
    /// <summary>
    /// Create Participant when lead is created and restrict Account Creation
    /// </summary>
    public class LeadQualifyProcess : IPlugin
    {

        Entity leadEnt = new Entity();
        Guid participantId = Guid.Empty;

        IPluginExecutionContext context = null;
        IOrganizationServiceFactory factory = null;
        IOrganizationService service = null;
        ITracingService tracing = null;
        /// <summary>
        /// First Method to execute
        /// </summary>
        /// <param name="serviceProvider"></param>
        public void Execute(IServiceProvider serviceProvider)
        {
            #region CRM Declaration
            context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            service = factory.CreateOrganizationService(context.UserId);
            tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            string functionName = "Execute";
            #endregion
            try
            {
                tracing.Trace("Inside the : " + functionName);
                tracing.Trace("Entity Name : " + context.PrimaryEntityName.ToLower());
                if (context.PrimaryEntityName.ToLower() == "lead")
                {
                    //Get the Lead details
                    leadEnt = GetLeadData(context, service, tracing);
                    //Validate Lead entity
                    if (leadEnt != null && leadEnt.Contains("hsb_hsbdivision") && leadEnt.Attributes["hsb_hsbdivision"] != null ) 
                    {
                        if(((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 1 || ((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 4) // 1= D2B 4=IRC
                        {
                          tracing.Trace("hsb_participantid Contains: " + leadEnt.Contains("hsb_participantid"));
                          tracing.Trace("hsb_businessname Contains: " + leadEnt.Contains("hsb_businessname"));
                          //Check if participant is allready present
                          if (leadEnt.Contains("hsb_businessname") && leadEnt.Attributes["hsb_businessname"] != null && leadEnt.Contains("hsb_participantid") == false)
                          {
                            tracing.Trace("hsb_businessname: " + leadEnt.Attributes["hsb_businessname"]);
                            /// Create Participant Records
                            participantId = CreateParticipant(leadEnt, context, service, tracing);

                            //Update the participant on lead
                            UpdateLeadWithParticipant(participantId, context, service, tracing);

                            //associate IRD Direct account with participant
                            if (((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 4) //check if HSB division is IRC
                            {
                                AssociateAccountWithParticipant(participantId, context, service, tracing);
                            }
                          }
                          //Update the participant on lead
                          UpdateLeadWithParticipant(participantId, context, service, tracing);

                          tracing.Trace("Process Completed");
                        }else if(((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 0) //check if HSB Division is custom
                        {
                            //Retrieve custom lead validation fields data
                            ColumnSet columns = new ColumnSet("parentcontactid", "hsb_effectivedate", "hsb_estimatedpremium", "hsb_leadtype");
                            Entity customLead = service.Retrieve(leadEnt.LogicalName , (Guid)leadEnt.Id, columns);

                            //error messages to show
                            String errorMessage1 = "Please Provide \"Estimated Premium\", \"Effective Date\" and \"Client Contact\" before qualifying the lead record.";
                            String errorMessage2 = "\"Contact\" and \"Potential New Account\" are not valid Lead Types for lead qualification.";

                            //check if client contact, effective date and estimated premium fields are empty
                            if (!customLead.Contains("parentcontactid") || !customLead.Contains("hsb_effectivedate") || !customLead.Contains("hsb_estimatedpremium"))
                            {
                                throw new InvalidPluginExecutionException(errorMessage1);
                            }

                            //check if lead type is contact or potential new account
                            if (customLead.Contains("hsb_leadtype") && (((OptionSetValue)(customLead.Attributes["hsb_leadtype"])).Value == 1 || ((OptionSetValue)(customLead.Attributes["hsb_leadtype"])).Value == 2))
                            {
                                throw new InvalidPluginExecutionException(errorMessage2);
                            }                  
                        }
                    }
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }

        /// <summary>
        /// Update Participant Record - Associate Account
        /// </summary>
        /// <param name="participantId">Guid participantId</param>
        /// <param name="context">IPluginExecutionContext context</param>
        /// <param name="service">IOrganizationService service</param>
        /// <param name="tracing">ITracingService tracing</param>
        public void AssociateAccountWithParticipant(Guid participantId, IPluginExecutionContext context, IOrganizationService service, ITracingService tracing)
        {
            //Entity ClientCompanyAccount = null;
            string functionName = "AssociateAccountWithParticipant";
            try
            {
                tracing.Trace("Associating Client Company Account");
                AssociateRequest req = new AssociateRequest
                {
                    Target = new EntityReference("msdyn_functionallocation", participantId),
                    RelatedEntities = new EntityReferenceCollection
                    {
                    new EntityReference("account", new Guid("d3f28ba2-c266-ee11-9ae7-6045bd07e840")) //Direct IRC Account 
                    },
                    Relationship = new Relationship("msdyn_msdyn_functionallocation_account")
                };
                // Execute the request.
                service.Execute(req);
                tracing.Trace("Post Execute Associate Request Associating Client Company Account");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }

        /// <summary>
        /// Update Lead Record
        /// </summary>
        /// <param name="participantId">Guid participantId</param>
        /// <param name="context">IPluginExecutionContext context</param>
        /// <param name="service">IOrganizationService service</param>
        /// <param name="tracing">ITracingService tracing</param>
        public void UpdateLeadWithParticipant(Guid participantId, IPluginExecutionContext context, IOrganizationService service, ITracingService tracing)
        {
            string functionName = "UpdateLeadWithParticipant";
            Entity lead = new Entity("lead");
            EntityReference leadEntRef = null;
            Guid contactTypeid = new Guid("6ced3c44-4112-ed11-b83d-000d3a3bb54f");
            Guid roleType = new Guid("3f970bb1-e102-ed11-82e4-0022480997de");
            try
            {
                // getting the leadid
                leadEntRef = (EntityReference)context.InputParameters["LeadId"];
                tracing.Trace("Inside the " + functionName);
                tracing.Trace("Lead Id:" + leadEntRef.Id);
                lead.Id = leadEntRef.Id;
                tracing.Trace("participantId:" + participantId);
                if (participantId != Guid.Empty)
                {
                    lead["hsb_participantid"] = new EntityReference("msdyn_functionallocation", participantId);
                }

                lead["hsb_contacttype"] = new EntityReference("hsb_contacttype", contactTypeid);
                lead["hsb_roletype"] = new EntityReference("hsb_roletype", roleType);
                service.Update(lead);
                tracing.Trace("Lead updated");

            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }

        /// <summary>
        /// Get the Lead details
        /// </summary>
        /// <param name="context">IPluginExecutionContext context</param>
        /// <param name="service">IOrganizationService service</param>
        /// <param name="trace">ITracingService trace</param>
        public Entity GetLeadData(IPluginExecutionContext context, IOrganizationService service, ITracingService tracing)
        {
            string functionName = "GetLeaddata";
            Entity leadEnt = null;
            EntityReference leadEntRef = null;
            try
            {
                // getting the leadid
                leadEntRef = (EntityReference)context.InputParameters["LeadId"];
                tracing.Trace("Inside the:" + functionName);
                tracing.Trace("EntityName:" + leadEntRef.LogicalName);
                tracing.Trace("leadRef:" + leadEntRef.Id);
                leadEnt = service.Retrieve(leadEntRef.LogicalName, (Guid)(leadEntRef.Id), new ColumnSet("companyname", "telephone1", "address1_line1", "address1_line2", "address1_line2", "address1_line3", "address1_city", "address1_postalcode", "hsb_country", "hsb_hsbdivision", "hsb_participantid", "hsb_businessname", "emailaddress1", "hsb_stateid", "hsb_businesstypeid"));
                tracing.Trace("Lead data Found");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            return leadEnt;
        }

        /// <summary>
        /// Create Participant Records
        /// </summary>
        /// <param name="lead">Entity lead</param>
        /// <param name="context">IPluginExecutionContext context</param>
        /// <param name="service">IOrganizationService service</param>
        /// <param name="trace">ITracingService tracing</param>
        public Guid CreateParticipant(Entity lead, IPluginExecutionContext context, IOrganizationService service, ITracingService tracing)
        {

            string functionName = "CreateParticipant";
            Entity participant = new Entity("msdyn_functionallocation");

            tracing.Trace("Inside the " + functionName);
            Guid participantId = Guid.Empty;
            try
            {
                #region Map Participant Fields
                //Get Company
                if (lead.Contains("hsb_businessname") && lead.Attributes["hsb_businessname"] != null)
                {
                    tracing.Trace("hsb_businessname" + lead.Attributes["hsb_businessname"]);
                    //set participant Name
                    participant["msdyn_name"] = lead.Attributes["hsb_businessname"];
                }
                //Get Business Phone
                if (lead.Contains("telephone1") && lead.Attributes["telephone1"] != null)
                {
                    tracing.Trace("telephone1" + lead.Attributes["telephone1"]);
                    //Set Business Phone
                    participant["hsb_shippingphonenumber"] = lead.Attributes["telephone1"];
                }
                //Get Email
                if (lead.Contains("emailaddress1") && lead.Attributes["emailaddress1"] != null)
                {
                    tracing.Trace("emailaddress1" + lead.Attributes["emailaddress1"]);
                    //Set Email
                    participant["hsb_emailaddress"] = lead.Attributes["emailaddress1"];
                }
                //Get hsb division
                if (lead.Contains("hsb_hsbdivision") && lead.Attributes["hsb_hsbdivision"] != null)
                {
                    tracing.Trace("hsb_hsbdivision" + lead.Attributes["hsb_hsbdivision"]);
                    //Set hsb division
                    participant["hsb_hsbdivision"] = lead.Attributes["hsb_hsbdivision"];
                }
                if (lead.Contains("hsb_hsbdivision") && lead.Attributes["hsb_hsbdivision"] != null && ((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 1) //check if hsb division is d2b if yes then map to custom adddress
                {
                    //Get address line 1
                    if (lead.Contains("address1_line1") && lead.Attributes["address1_line1"] != null)
                    {
                        tracing.Trace("address1_line1" + lead.Attributes["address1_line1"]);
                        //Set address line 1
                        participant["hsb_addressline1"] = lead.Attributes["address1_line1"];
                    }
                    //Get address line 2
                    if (lead.Contains("address1_line2") && lead.Attributes["address1_line2"] != null)
                    {
                        tracing.Trace("address1_line2" + lead.Attributes["address1_line2"]);
                        //Set address line 2
                        participant["hsb_addressline2"] = lead.Attributes["address1_line2"];
                    }
                    //Get address line 3
                    if (lead.Contains("address1_line3") && lead.Attributes["address1_line3"] != null)
                    {
                        tracing.Trace("address1_line3" + lead.Attributes["address1_line3"]);
                        //Set address line 3
                        participant["hsb_addressline3"] = lead.Attributes["address1_line3"];
                    }
                    //Get postal code
                    if (lead.Contains("address1_city") && lead.Attributes["address1_city"] != null)
                    {
                        tracing.Trace("address1_city" + lead.Attributes["address1_city"]);
                        //Set postal code
                        participant["hsb_city"] = lead.Attributes["address1_city"];
                    }
                    //Get City
                    if (lead.Contains("address1_postalcode") && lead.Attributes["address1_postalcode"] != null)
                    {
                        tracing.Trace("address1_postalcode" + lead.Attributes["address1_postalcode"]);
                        //Set city
                        participant["hsb_postalcode"] = lead.Attributes["address1_postalcode"];
                    }
                    //Get state
                    if (lead.Contains("hsb_stateid") && lead.Attributes["hsb_stateid"] != null)
                    {
                        tracing.Trace("Logical Name:" + ((EntityReference)lead.Attributes["hsb_stateid"]).LogicalName);
                        tracing.Trace("record id:" + ((EntityReference)lead.Attributes["hsb_stateid"]).Id);
                        //Set state
                        participant["hsb_address1state"] = new EntityReference(((EntityReference)lead.Attributes["hsb_stateid"]).LogicalName, ((EntityReference)lead.Attributes["hsb_stateid"]).Id);
                    }
                    //Get country
                    if (lead.Contains("hsb_country") && lead.Attributes["hsb_country"] != null)
                    {
                        tracing.Trace("Logical Name:" + ((EntityReference)lead.Attributes["hsb_country"]).LogicalName);
                        tracing.Trace("record id:" + ((EntityReference)lead.Attributes["hsb_country"]).Id);
                        //Set country
                        participant["hsb_country"] = new EntityReference(((EntityReference)lead.Attributes["hsb_country"]).LogicalName, ((EntityReference)lead.Attributes["hsb_country"]).Id);
                    }
                }

                if (lead.Contains("hsb_hsbdivision") && lead.Attributes["hsb_hsbdivision"] != null && ((OptionSetValue)(leadEnt.Attributes["hsb_hsbdivision"])).Value == 4) //check if hsb division is engineering insurance if yes then map to ootb adddress
                {
                    //Get address line 1
                    if (lead.Contains("address1_line1") && lead.Attributes["address1_line1"] != null)
                    {
                        tracing.Trace("address1_line1" + lead.Attributes["address1_line1"]);
                        //Set address line 1
                        participant["msdyn_address1"] = lead.Attributes["address1_line1"];
                    }
                    //Get address line 2
                    if (lead.Contains("address1_line2") && lead.Attributes["address1_line2"] != null)
                    {
                        tracing.Trace("address1_line2" + lead.Attributes["address1_line2"]);
                        //Set address line 2
                        participant["msdyn_address2"] = lead.Attributes["address1_line2"];
                    }
                    //Get address line 3
                    if (lead.Contains("address1_line3") && lead.Attributes["address1_line3"] != null)
                    {
                        tracing.Trace("address1_line3" + lead.Attributes["address1_line3"]);
                        //Set address line 3
                        participant["msdyn_address3"] = lead.Attributes["address1_line3"];
                    }
                    //Get postal code
                    if (lead.Contains("address1_city") && lead.Attributes["address1_city"] != null)
                    {
                        tracing.Trace("address1_city" + lead.Attributes["address1_city"]);
                        //Set postal code
                        participant["msdyn_city"] = lead.Attributes["address1_city"];
                    }
                    //Get City
                    if (lead.Contains("address1_postalcode") && lead.Attributes["address1_postalcode"] != null)
                    {
                        tracing.Trace("address1_postalcode" + lead.Attributes["address1_postalcode"]);
                        //Set city
                        participant["msdyn_postalcode"] = lead.Attributes["address1_postalcode"];
                    }
                    //Get state
                    if (lead.Contains("hsb_stateid") && lead.Attributes["hsb_stateid"] != null)
                    {
                        tracing.Trace("Logical Name:" + ((EntityReference)lead.Attributes["hsb_stateid"]).LogicalName);
                        tracing.Trace("record id:" + ((EntityReference)lead.Attributes["hsb_stateid"]).Id);
                        //Set state
                        participant["hsb_stateprovinceid"] = new EntityReference(((EntityReference)lead.Attributes["hsb_stateid"]).LogicalName, ((EntityReference)lead.Attributes["hsb_stateid"]).Id);
                    }
                    //Get country
                    if (lead.Contains("hsb_country") && lead.Attributes["hsb_country"] != null)
                    {
                        tracing.Trace("Logical Name:" + ((EntityReference)lead.Attributes["hsb_country"]).LogicalName);
                        tracing.Trace("record id:" + ((EntityReference)lead.Attributes["hsb_country"]).Id);
                        //Set country
                        participant["hsb_address1country"] = new EntityReference(((EntityReference)lead.Attributes["hsb_country"]).LogicalName, ((EntityReference)lead.Attributes["hsb_country"]).Id);
                    }
                    //Business Type
                    if (lead.Contains("hsb_businesstypeid") && lead.Attributes["hsb_businesstypeid"] != null)
                    {
                        tracing.Trace("Logical Name:" + ((EntityReference)lead.Attributes["hsb_businesstypeid"]).LogicalName);
                        tracing.Trace("record id:" + ((EntityReference)lead.Attributes["hsb_businesstypeid"]).Id);
                        //Set country
                        participant["hsb_businesstype"] = new EntityReference(((EntityReference)lead.Attributes["hsb_businesstypeid"]).LogicalName, ((EntityReference)lead.Attributes["hsb_businesstypeid"]).Id);
                    }
                   
                }

                #endregion
                //Create Participant Record and Get its guid
                participantId = service.Create(participant);
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            return participantId;
        }

        /// <summary>
        /// Restrict Account Creation While Qulifing the Lead
        /// </summary>
        /// <param name="context">IPluginExecutionContext context</param>
        /// <param name="service">IOrganizationService service</param>
        /// <param name="tracing">ITracingService tracing</param>
        public void RestrictAccountWhileQualifingLead(IPluginExecutionContext context, IOrganizationService service, ITracingService tracing)
        {
            string functionName = "RestrictAccountWhileQualifingLead";
            tracing.Trace("Inside the " + functionName);
            try
            {
                // Take the input parameters
                var isCreateAccount = (bool)context.InputParameters["CreateAccount"];
                tracing.Trace("isCreateAccount:" + isCreateAccount);
                var isCreateContact = (bool)context.InputParameters["CreateContact"];
                tracing.Trace("isCreateContact:" + isCreateContact);
                var isCreateOpportunity = (bool)context.InputParameters["CreateOpportunity"];
                tracing.Trace("isCreateOpportunity:" + isCreateOpportunity);
                
                // Add custom logic to determine if you want to create a contact/account/opportunity.
                // The below code will skip account creation during the qualify process.
                context.InputParameters["CreateAccount"] = false;
                context.InputParameters["CreateContact"] = true;
                context.InputParameters["CreateOpportunity"] = true;
                
                tracing.Trace("RestrictAccountWhileQualifingLead function completed");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                tracing.Trace(functionName + ":" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}

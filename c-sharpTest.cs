using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Fakes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ServiceModel;

[TestFixture]
public class MyPluginTests
{
    [Test]
    public void RestrictAccountWhileQualifingLead_Should_Set_CreateAccount_To_False()
    {
        // Arrange
        var plugin = new MyPlugin();
        var context = new PluginExecutionContextFake();
        var service = new OrganizationServiceFake();
        var tracing = new TracingServiceFake();
        context.InputParameters["CreateAccount"] = true;

        // Act
        plugin.RestrictAccountWhileQualifingLead(context, service, tracing);

        // Assert
        Assert.IsFalse((bool)context.InputParameters["CreateAccount"]);
    }

    [Test]
    public void RestrictAccountWhileQualifingLead_Should_Set_CreateContact_And_CreateOpportunity_To_True()
    {
        // Arrange
        var plugin = new MyPlugin();
        var context = new PluginExecutionContextFake();
        var service = new OrganizationServiceFake();
        var tracing = new TracingServiceFake();
        context.InputParameters["CreateContact"] = false;
        context.InputParameters["CreateOpportunity"] = false;

        // Act
        plugin.RestrictAccountWhileQualifingLead(context, service, tracing);

        // Assert
        Assert.IsTrue((bool)context.InputParameters["CreateContact"]);
        Assert.IsTrue((bool)context.InputParameters["CreateOpportunity"]);
    }

    [Test]
    public void RestrictAccountWhileQualifingLead_Should_Log_Exceptions()
    {
        // Arrange
        var plugin = new MyPlugin();
        var context = new PluginExecutionContextFake();
        var service = new OrganizationServiceFake();
        var tracing = new TracingServiceFake();
        context.InputParameters["CreateAccount"] = true;
        var expectedExceptionMessage = "An error occurred.";

        // Act & Assert
        Assert.Throws<InvalidPluginExecutionException>(() => plugin.RestrictAccountWhileQualifingLead(context, service, tracing));
        Assert.AreEqual(expectedExceptionMessage, tracing.Traces[0]);
    }
}

public class PluginExecutionContextFake : IPluginExecutionContext
{
    public Dictionary<string, object> InputParameters { get; set; } = new Dictionary<string, object>();
    // Implement other properties and methods as needed
}

public class OrganizationServiceFake : IOrganizationService
{
    // Implement IOrganizationService methods as needed
}

public class TracingServiceFake : ITracingService
{
    public List<string> Traces { get; set; } = new List<string>();

    public void Trace(string message)
    {
        Traces.Add(message);
    }
}
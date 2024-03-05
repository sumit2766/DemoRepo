 var instantiateEmailTemplate = function (tempId, type, caseId, formContext) {
       
        var req = new XMLHttpRequest();
        req.open("POST", context.getClientUrl() + requestUrl, true);
        req.setRequestHeader("OData-MaxVersion", "4.0");
        req.setRequestHeader("OData-Version", "4.0");
        req.setRequestHeader("Accept", "application/json");
        req.setRequestHeader("Content-Type", "application/json; charset=utf-8");

        // Add CSRF token to the request headers
        var csrfToken = getCSRFToken(); // Replace with your CSRF token retrieval logic
        req.setRequestHeader("X-CSRF-Token", csrfToken);

        req.onreadystatechange = function () {
            if (this.readyState === 4) {
                req.onreadystatechange = null;
                if (this.status === 200) {
                    var result = JSON.parse(this.response);
                    //DO SOMETHING WITH THE RESPONSE HERE
                    var subject = result.value[0].subject;
                    var body = result.value[0].description;
                    //var desc = stripHtml(body);
                    Xrm.Utility.openEntityForm("hsb_interaction", formContext.data.entity.getId());
                    openEmailForm(subject, body, formContext);
                } else {
                    var errorText = this.responseText;
                    alert(errorText);
                }
            }
        };
        req.send(JSON.stringify(parameters));
    }
using System;

using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Azure.AI.FormRecognizer.Training;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace quickstart {
  class Program {

    private static readonly string endpoint = "<your api endpoint>";
    private static readonly string apiKey = "<your Form Recognizer key>";
    private static readonly AzureKeyCredential credential = new AzureKeyCredential(apiKey);

    static string trainingDataUrl = "<SAS-URL-of-your-form-folder-in-blob-storage>";
    static string formUrl = "<SAS-URL-of-a-form-in-blob-storage>";
    static string receiptUrl = "https://docs.microsoft.com/azure/cognitive-services/form-recognizer/media" + "/contoso-allinone.jpg";
    static string bcUrl = "https://raw.githubusercontent.com/Azure/azure-sdk-for-python/master/sdk/formrecognizer/azure-ai-formrecognizer/samples/sample_forms/business_cards/business-card-english.jpg";
    static string invoiceUrl = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/simple-invoice.png";
    static string idUrl = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/id-license.jpg";
    
    static void Main(string[] args) {

      FormRecognizerClient recognizerClient = AuthenticateClient();

      FormTrainingClient trainingclient = AuthenticateTrainingClient();

      var recognizeContent = RecognizeContent(recognizerClient);
      Task.WaitAll(recognizeContent);

      var analyzereceipt = AnalyzeReceipt(recognizerClient, receiptUrl);
      Task.WaitAll(analyzereceipt);

      var analyzebusinesscard = AnalyzeBusinessCard(recognizerClient, bcUrl);
      Task.WaitAll(analyzebusinesscard);

      var analyzeinvoice = AnalyzeInvoice(recognizerClient, invoiceUrl);
      Task.WaitAll(analyzeinvoice);

      var analyzeid = AnalyzeId(recognizerClient, idUrl);
      Task.WaitAll(analyzeid);

      var trainmodel = TrainModel(trainingclient, trainingDataUrl);
      Task.WaitAll(trainmodel);

      var trainmodelwithlabels = TrainModelWithLabelsAsync(trainingclient, trainingDataUrl);
      Task.WaitAll(trainmodelwithlabels);

      var analyzeform = AnalyzePdfForm(recognizerClient, trainmodel.Result, formUrl);
      Task.WaitAll(analyzeform);

      var managemodels = ManageModels(trainingclient, trainingDataUrl);
      Task.WaitAll(managemodels);
    }

    private static FormRecognizerClient AuthenticateClient() {
      var credential = new AzureKeyCredential(apiKey);
      var client = new FormRecognizerClient(new Uri(endpoint), credential);
      return client;
    }

    static private FormTrainingClient AuthenticateTrainingClient() {
      var credential = new AzureKeyCredential(apiKey);
      var client = new FormTrainingClient(new Uri(endpoint), credential);
      return client;
    }

    private static async Task RecognizeContent(FormRecognizerClient recognizerClient) {
      var invoiceUri = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/simple-invoice.png";
      FormPageCollection formPages = await recognizerClient
        .StartRecognizeContentFromUri(new Uri(invoiceUri))
        .WaitForCompletionAsync();
      foreach(FormPage page in formPages) {
        Console.WriteLine($"Form Page {page.PageNumber} has {page.Lines.Count} lines.");

        for (int i = 0; i < page.Lines.Count; i++) {
          FormLine line = page.Lines[i];
          Console.WriteLine($"    Line {i} has {line.Words.Count} word{(line.Words.Count > 1 ? "
            s " : "
            ")}, and text: '{line.Text}'.");
        }

        for (int i = 0; i < page.Tables.Count; i++) {
          FormTable table = page.Tables[i];
          Console.WriteLine($"Table {i} has {table.RowCount} rows and {table.ColumnCount} columns.");
          foreach(FormTableCell cell in table.Cells) {
            Console.WriteLine($"    Cell ({cell.RowIndex}, {cell.ColumnIndex}) contains text: '{cell.Text}'.");
          }
        }
      }
    }

    private static async Task AnalyzeReceipt(FormRecognizerClient recognizerClient, string receiptUri) {
      RecognizedFormCollection receipts = await recognizerClient.StartRecognizeReceiptsFromUri(new Uri(receiptUrl)).WaitForCompletionAsync();

      foreach(RecognizedForm receipt in receipts) {
        FormField merchantNameField;
        if (receipt.Fields.TryGetValue("MerchantName", out merchantNameField)) {
          if (merchantNameField.Value.ValueType == FieldValueType.String) {
            string merchantName = merchantNameField.Value.AsString();

            Console.WriteLine($"Merchant Name: '{merchantName}', with confidence {merchantNameField.Confidence}");
          }
        }

        FormField transactionDateField;
        if (receipt.Fields.TryGetValue("TransactionDate", out transactionDateField)) {
          if (transactionDateField.Value.ValueType == FieldValueType.Date) {
            DateTime transactionDate = transactionDateField.Value.AsDate();

            Console.WriteLine($"Transaction Date: '{transactionDate}', with confidence {transactionDateField.Confidence}");
          }
        }

        FormField itemsField;
        if (receipt.Fields.TryGetValue("Items", out itemsField)) {
          if (itemsField.Value.ValueType == FieldValueType.List) {
            foreach(FormField itemField in itemsField.Value.AsList()) {
              Console.WriteLine("Item:");

              if (itemField.Value.ValueType == FieldValueType.Dictionary) {
                IReadOnlyDictionary < string, FormField > itemFields = itemField.Value.AsDictionary();

                FormField itemNameField;
                if (itemFields.TryGetValue("Name", out itemNameField)) {
                  if (itemNameField.Value.ValueType == FieldValueType.String) {
                    string itemName = itemNameField.Value.AsString();

                    Console.WriteLine($"    Name: '{itemName}', with confidence {itemNameField.Confidence}");
                  }
                }

                FormField itemTotalPriceField;
                if (itemFields.TryGetValue("TotalPrice", out itemTotalPriceField)) {
                  if (itemTotalPriceField.Value.ValueType == FieldValueType.Float) {
                    float itemTotalPrice = itemTotalPriceField.Value.AsFloat();

                    Console.WriteLine($"    Total Price: '{itemTotalPrice}', with confidence {itemTotalPriceField.Confidence}");
                  }
                }
              }
            }
          }
        }
        FormField totalField;
        if (receipt.Fields.TryGetValue("Total", out totalField)) {
          if (totalField.Value.ValueType == FieldValueType.Float) {
            float total = totalField.Value.AsFloat();

            Console.WriteLine($"Total: '{total}', with confidence '{totalField.Confidence}'");
          }
        }
      }
    }

    private static async Task AnalyzeBusinessCard(FormRecognizerClient recognizerClient, string bcUrl) {
      RecognizedFormCollection businessCards = await recognizerClient.StartRecognizeBusinessCardsFromUriAsync(new Uri(bcUrl)).WaitForCompletionAsync();
      foreach(RecognizedForm businessCard in businessCards) {
        FormField ContactNamesField;
        if (businessCard.Fields.TryGetValue("ContactNames", out ContactNamesField)) {
          if (ContactNamesField.Value.ValueType == FieldValueType.List) {
            foreach(FormField contactNameField in ContactNamesField.Value.AsList()) {
              Console.WriteLine($"Contact Name: {contactNameField.ValueData.Text}");

              if (contactNameField.Value.ValueType == FieldValueType.Dictionary) {
                IReadOnlyDictionary < string,
                  FormField > contactNameFields = contactNameField.Value.AsDictionary();

                FormField firstNameField;
                if (contactNameFields.TryGetValue("FirstName", out firstNameField)) {
                  if (firstNameField.Value.ValueType == FieldValueType.String) {
                    string firstName = firstNameField.Value.AsString();

                    Console.WriteLine($"    First Name: '{firstName}', with confidence {firstNameField.Confidence}");
                  }
                }

                FormField lastNameField;
                if (contactNameFields.TryGetValue("LastName", out lastNameField)) {
                  if (lastNameField.Value.ValueType == FieldValueType.String) {
                    string lastName = lastNameField.Value.AsString();

                    Console.WriteLine($"    Last Name: '{lastName}', with confidence {lastNameField.Confidence}");
                  }
                }
              }
            }
          }
        }

        FormField jobTitlesFields;
        if (businessCard.Fields.TryGetValue("JobTitles", out jobTitlesFields)) {
          if (jobTitlesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField jobTitleField in jobTitlesFields.Value.AsList()) {
              if (jobTitleField.Value.ValueType == FieldValueType.String) {
                string jobTitle = jobTitleField.Value.AsString();

                Console.WriteLine($"  Job Title: '{jobTitle}', with confidence {jobTitleField.Confidence}");
              }
            }
          }
        }

        FormField departmentFields;
        if (businessCard.Fields.TryGetValue("Departments", out departmentFields)) {
          if (departmentFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField departmentField in departmentFields.Value.AsList()) {
              if (departmentField.Value.ValueType == FieldValueType.String) {
                string department = departmentField.Value.AsString();

                Console.WriteLine($"  Department: '{department}', with confidence {departmentField.Confidence}");
              }
            }
          }
        }

        FormField emailFields;
        if (businessCard.Fields.TryGetValue("Emails", out emailFields)) {
          if (emailFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField emailField in emailFields.Value.AsList()) {
              if (emailField.Value.ValueType == FieldValueType.String) {
                string email = emailField.Value.AsString();

                Console.WriteLine($"  Email: '{email}', with confidence {emailField.Confidence}");
              }
            }
          }
        }

        FormField websiteFields;
        if (businessCard.Fields.TryGetValue("Websites", out websiteFields)) {
          if (websiteFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField websiteField in websiteFields.Value.AsList()) {
              if (websiteField.Value.ValueType == FieldValueType.String) {
                string website = websiteField.Value.AsString();

                Console.WriteLine($"  Website: '{website}', with confidence {websiteField.Confidence}");
              }
            }
          }
        }

        FormField mobilePhonesFields;
        if (businessCard.Fields.TryGetValue("MobilePhones", out mobilePhonesFields)) {
          if (mobilePhonesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField mobilePhoneField in mobilePhonesFields.Value.AsList()) {
              if (mobilePhoneField.Value.ValueType == FieldValueType.PhoneNumber) {
                string mobilePhone = mobilePhoneField.Value.AsPhoneNumber();

                Console.WriteLine($"  Mobile phone number: '{mobilePhone}', with confidence {mobilePhoneField.Confidence}");
              }
            }
          }
        }

        FormField otherPhonesFields;
        if (businessCard.Fields.TryGetValue("OtherPhones", out otherPhonesFields)) {
          if (otherPhonesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField otherPhoneField in otherPhonesFields.Value.AsList()) {
              if (otherPhoneField.Value.ValueType == FieldValueType.PhoneNumber) {
                string otherPhone = otherPhoneField.Value.AsPhoneNumber();

                Console.WriteLine($"  Other phone number: '{otherPhone}', with confidence {otherPhoneField.Confidence}");
              }
            }
          }
        }

        FormField faxesFields;
        if (businessCard.Fields.TryGetValue("Faxes", out faxesFields)) {
          if (faxesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField faxField in faxesFields.Value.AsList()) {
              if (faxField.Value.ValueType == FieldValueType.PhoneNumber) {
                string fax = faxField.Value.AsPhoneNumber();

                Console.WriteLine($"  Fax phone number: '{fax}', with confidence {faxField.Confidence}");
              }
            }
          }
        }

        FormField addressesFields;
        if (businessCard.Fields.TryGetValue("Addresses", out addressesFields)) {
          if (addressesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField addressField in addressesFields.Value.AsList()) {
              if (addressField.Value.ValueType == FieldValueType.String) {
                string address = addressField.Value.AsString();

                Console.WriteLine($"  Address: '{address}', with confidence {addressField.Confidence}");
              }
            }
          }
        }

        FormField companyNamesFields;
        if (businessCard.Fields.TryGetValue("CompanyNames", out companyNamesFields)) {
          if (companyNamesFields.Value.ValueType == FieldValueType.List) {
            foreach(FormField companyNameField in companyNamesFields.Value.AsList()) {
              if (companyNameField.Value.ValueType == FieldValueType.String) {
                string companyName = companyNameField.Value.AsString();

                Console.WriteLine($"  Company name: '{companyName}', with confidence {companyNameField.Confidence}");
              }
            }
          }
        }
      }
    }

    private static async Task AnalyzeInvoice(FormRecognizerClient recognizerClient, string invoiceUrl) {
      var options = new RecognizeInvoicesOptions() {
        Locale = "en-US"
      };
      RecognizedFormCollection invoices = await recognizerClient.StartRecognizeInvoicesFromUriAsync(new Uri(invoiceUrl), options).WaitForCompletionAsync();

      RecognizedForm invoice = invoices[0];

      FormField invoiceIdField;
      if (invoice.Fields.TryGetValue("InvoiceId", out invoiceIdField)) {
        if (invoiceIdField.Value.ValueType == FieldValueType.String) {
          string invoiceId = invoiceIdField.Value.AsString();
          Console.WriteLine($"    Invoice Id: '{invoiceId}', with confidence {invoiceIdField.Confidence}");
        }
      }

      FormField invoiceDateField;
      if (invoice.Fields.TryGetValue("InvoiceDate", out invoiceDateField)) {
        if (invoiceDateField.Value.ValueType == FieldValueType.Date) {
          DateTime invoiceDate = invoiceDateField.Value.AsDate();
          Console.WriteLine($"    Invoice Date: '{invoiceDate}', with confidence {invoiceDateField.Confidence}");
        }
      }

      FormField dueDateField;
      if (invoice.Fields.TryGetValue("DueDate", out dueDateField)) {
        if (dueDateField.Value.ValueType == FieldValueType.Date) {
          DateTime dueDate = dueDateField.Value.AsDate();
          Console.WriteLine($"    Due Date: '{dueDate}', with confidence {dueDateField.Confidence}");
        }
      }

      FormField vendorNameField;
      if (invoice.Fields.TryGetValue("VendorName", out vendorNameField)) {
        if (vendorNameField.Value.ValueType == FieldValueType.String) {
          string vendorName = vendorNameField.Value.AsString();
          Console.WriteLine($"    Vendor Name: '{vendorName}', with confidence {vendorNameField.Confidence}");
        }
      }

      FormField vendorAddressField;
      if (invoice.Fields.TryGetValue("VendorAddress", out vendorAddressField)) {
        if (vendorAddressField.Value.ValueType == FieldValueType.String) {
          string vendorAddress = vendorAddressField.Value.AsString();
          Console.WriteLine($"    Vendor Address: '{vendorAddress}', with confidence {vendorAddressField.Confidence}");
        }
      }

      FormField customerNameField;
      if (invoice.Fields.TryGetValue("CustomerName", out customerNameField)) {
        if (customerNameField.Value.ValueType == FieldValueType.String) {
          string customerName = customerNameField.Value.AsString();
          Console.WriteLine($"    Customer Name: '{customerName}', with confidence {customerNameField.Confidence}");
        }
      }

      FormField customerAddressField;
      if (invoice.Fields.TryGetValue("CustomerAddress", out customerAddressField)) {
        if (customerAddressField.Value.ValueType == FieldValueType.String) {
          string customerAddress = customerAddressField.Value.AsString();
          Console.WriteLine($"    Customer Address: '{customerAddress}', with confidence {customerAddressField.Confidence}");
        }
      }

      FormField customerAddressRecipientField;
      if (invoice.Fields.TryGetValue("CustomerAddressRecipient", out customerAddressRecipientField)) {
        if (customerAddressRecipientField.Value.ValueType == FieldValueType.String) {
          string customerAddressRecipient = customerAddressRecipientField.Value.AsString();
          Console.WriteLine($"    Customer address recipient: '{customerAddressRecipient}', with confidence {customerAddressRecipientField.Confidence}");
        }
      }

      FormField invoiceTotalField;
      if (invoice.Fields.TryGetValue("InvoiceTotal", out invoiceTotalField)) {
        if (invoiceTotalField.Value.ValueType == FieldValueType.Float) {
          float invoiceTotal = invoiceTotalField.Value.AsFloat();
          Console.WriteLine($"    Invoice Total: '{invoiceTotal}', with confidence {invoiceTotalField.Confidence}");
        }
      }
    }

    private static async Task AnalyzeId(FormRecognizerClient recognizerClient, string idUrl) {
      RecognizeIdentityDocumentsOperation operation = await recognizerClient.StartRecognizeIdentityDocumentsFromUriAsync(new Uri(idUrl));
      Response < RecognizedFormCollection > operationResponse = await operation.WaitForCompletionAsync();
      RecognizedFormCollection identityDocuments = operationResponse.Value;

      RecognizedForm identityDocument = identityDocuments[0];

      if (identityDocument.Fields.TryGetValue("Address", out FormField addressField)) {
        if (addressField.Value.ValueType == FieldValueType.String) {
          string address = addressField.Value.AsString();
          Console.WriteLine($"Address: '{address}', with confidence {addressField.Confidence}");
        }
      }

      if (identityDocument.Fields.TryGetValue("CountryRegion", out FormField countryRegionField)) {
        if (countryRegionField.Value.ValueType == FieldValueType.CountryRegion) {
          string countryRegion = countryRegionField.Value.AsCountryRegion();
          Console.WriteLine($"CountryRegion: '{countryRegion}', with confidence {countryRegionField.Confidence}");
        }
      }

      if (identityDocument.Fields.TryGetValue("DateOfBirth", out FormField dateOfBirthField)) {
        if (dateOfBirthField.Value.ValueType == FieldValueType.Date) {
          DateTime dateOfBirth = dateOfBirthField.Value.AsDate();
          Console.WriteLine($"Date Of Birth: '{dateOfBirth}', with confidence {dateOfBirthField.Confidence}");
        }
      }

      if (identityDocument.Fields.TryGetValue("DateOfExpiration", out FormField dateOfExpirationField)) {
        if (dateOfExpirationField.Value.ValueType == FieldValueType.Date) {
          DateTime dateOfExpiration = dateOfExpirationField.Value.AsDate();
          Console.WriteLine($"Date Of Expiration: '{dateOfExpiration}', with confidence {dateOfExpirationField.Confidence}");
        }
      }

      if (identityDocument.Fields.TryGetValue("DocumentNumber", out FormField documentNumberField)) {
        if (documentNumberField.Value.ValueType == FieldValueType.String) {
          string documentNumber = documentNumberField.Value.AsString();
          Console.WriteLine($"Document Number: '{documentNumber}', with confidence {documentNumberField.Confidence}");
        }

        if (identityDocument.Fields.TryGetValue("FirstName", out FormField firstNameField)) {
          if (firstNameField.Value.ValueType == FieldValueType.String) {
            string firstName = firstNameField.Value.AsString();
            Console.WriteLine($"First Name: '{firstName}', with confidence {firstNameField.Confidence}");
          }
        }

        if (identityDocument.Fields.TryGetValue("LastName", out FormField lastNameField)) {
          if (lastNameField.Value.ValueType == FieldValueType.String) {
            string lastName = lastNameField.Value.AsString();
            Console.WriteLine($"Last Name: '{lastName}', with confidence {lastNameField.Confidence}");
          }
        }

        if (identityDocument.Fields.TryGetValue("Region", out FormField regionfield)) {
          if (regionfield.Value.ValueType == FieldValueType.String) {
            string region = regionfield.Value.AsString();
            Console.WriteLine($"Region: '{region}', with confidence {regionfield.Confidence}");
          }
        }
      }
    }

    private static async Task < String > TrainModel(
      FormTrainingClient trainingClient, string trainingDataUrl) {
      CustomFormModel model = await trainingClient
        .StartTrainingAsync(new Uri(trainingDataUrl), useTrainingLabels: false)
        .WaitForCompletionAsync();

      Console.WriteLine($"Custom Model Info:");
      Console.WriteLine($"    Model Id: {model.ModelId}");
      Console.WriteLine($"    Model Status: {model.Status}");
      Console.WriteLine($"    Training model started on: {model.TrainingStartedOn}");
      Console.WriteLine($"    Training model completed on: {model.TrainingCompletedOn}");

      foreach(CustomFormSubmodel submodel in model.Submodels) {
        Console.WriteLine($"Submodel Form Type: {submodel.FormType}");
        foreach(CustomFormModelField field in submodel.Fields.Values) {
          Console.Write($"    FieldName: {field.Name}");
          if (field.Label != null) {
            Console.Write($", FieldLabel: {field.Label}");
          }
          Console.WriteLine("");
        }
      }

      return model.ModelId;
    }

    private static async Task < String > TrainModelWithLabelsAsync(FormTrainingClient trainingClient, string trainingDataUrl) {
      CustomFormModel model = await trainingClient
        .StartTrainingAsync(new Uri(trainingDataUrl), useTrainingLabels: true)
        .WaitForCompletionAsync();

      Console.WriteLine($"Custom Model Info:");
      Console.WriteLine($"    Model Id: {model.ModelId}");
      Console.WriteLine($"    Model Status: {model.Status}");
      Console.WriteLine($"    Training model started on: {model.TrainingStartedOn}");
      Console.WriteLine($"    Training model completed on: {model.TrainingCompletedOn}");

      foreach(CustomFormSubmodel submodel in model.Submodels) {
        Console.WriteLine($"Submodel Form Type: {submodel.FormType}");
        foreach(CustomFormModelField field in submodel.Fields.Values) {
          Console.Write($"    FieldName: {field.Name}");
          if (field.Label != null) {
            Console.Write($", FieldLabel: {field.Label}");
          }
          Console.WriteLine("");
        }
      }
      return model.ModelId;
    }

    // Analyze PDF form data
    private static async Task AnalyzePdfForm(
      FormRecognizerClient recognizerClient, String modelId, string formUrl) {
      RecognizedFormCollection forms = await recognizerClient
        .StartRecognizeCustomFormsFromUri(modelId, new Uri(formUrl))
        .WaitForCompletionAsync();

      foreach(RecognizedForm form in forms) {
        Console.WriteLine($"Form of type: {form.FormType}");
        foreach(FormField field in form.Fields.Values) {
          Console.WriteLine($"Field '{field.Name}: ");

          if (field.LabelData != null) {
            Console.WriteLine($"    Label: '{field.LabelData.Text}");
          }

          Console.WriteLine($"    Value: '{field.ValueData.Text}");
          Console.WriteLine($"    Confidence: '{field.Confidence}");
        }
        Console.WriteLine("Table data:");
        foreach(FormPage page in form.Pages) {
          for (int i = 0; i < page.Tables.Count; i++) {
            FormTable table = page.Tables[i];
            Console.WriteLine($"Table {i} has {table.RowCount} rows and {table.ColumnCount} columns.");
            foreach(FormTableCell cell in table.Cells) {
              Console.WriteLine($"    Cell ({cell.RowIndex}, {cell.ColumnIndex}) contains {(cell.IsHeader ? "
                header " : "
                text ")}: '{cell.Text}'");
            }
          }
        }
      }
    }

    private static async Task ManageModels(FormTrainingClient trainingClient, string trainingFileUrl) {

      // Check number of models in the FormRecognizer account, 
      // and the maximum number of models that can be stored.
      AccountProperties accountProperties = trainingClient.GetAccountProperties();
      Console.WriteLine($"Account has {accountProperties.CustomModelCount} models.");
      Console.WriteLine($"It can have at most {accountProperties.CustomModelLimit} models.");

      Pageable < CustomFormModelInfo > models = trainingClient.GetCustomModels();

      foreach(CustomFormModelInfo modelInfo in models) {
        Console.WriteLine($"Custom Model Info:");
        Console.WriteLine($"    Model Id: {modelInfo.ModelId}");
        Console.WriteLine($"    Model Status: {modelInfo.Status}");
        Console.WriteLine($"    Training model started on: {modelInfo.TrainingStartedOn}");
        Console.WriteLine($"    Training model completed on: {modelInfo.TrainingCompletedOn}");
      }

      // Create a new model to store in the account
      CustomFormModel model = await trainingClient
        .StartTrainingAsync(new Uri(trainingFileUrl), useTrainingLabels: false)
        .WaitForCompletionAsync();

      // Get the model that was just created
      CustomFormModel modelCopy = trainingClient.GetCustomModel(model.ModelId);

      Console.WriteLine($"Custom Model {modelCopy.ModelId} recognizes the following form types:");

      foreach(CustomFormSubmodel submodel in modelCopy.Submodels) {
        Console.WriteLine($"Submodel Form Type: {submodel.FormType}");
        foreach(CustomFormModelField field in submodel.Fields.Values) {
          Console.Write($"    FieldName: {field.Name}");
          if (field.Label != null) {
            Console.Write($", FieldLabel: {field.Label}");
          }
          Console.WriteLine("");
        }
      }

      // Delete the model from the account.
      //trainingClient.DeleteModel(model.ModelId);
    }

  }
}

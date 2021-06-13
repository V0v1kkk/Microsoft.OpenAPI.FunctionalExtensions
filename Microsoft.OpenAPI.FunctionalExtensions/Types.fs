module Microsoft.OpenAPI.FunctionalExtensions.Types

type SpecificationFilePath = SpecificationFilePath of string

type LintingFail =
  | AnalyzingError of string
  | AnalyzingException of CaseDefinition:string * Exception:System.Exception 


type AlarmSubType = Tbd | Empty

// linter alarms
type EmptyOrBadOperationSummary = {OperationId: string; SubType: AlarmSubType}
type BadOperationDescription = {OperationId: string}
type EmptyOrBadParameterDescription = {OperationId: string; ParameterName: string; SubType: AlarmSubType}
type EmptyOrBadSchemaPropertyDescription = {SchemaName: string; PropertyName: string; SubType: AlarmSubType}

type LinterAlarm =
  | EmptyOrBadOperationSummary
  | BadOperationDescription
  | BadParameterDescription
  | BadSchemaPropertyDescription

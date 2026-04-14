# Form XML Summary

- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account Hierarchy Tile Form`
- Type: `6`

- Tabs: `1`
- Sections: `1`
- Controls: `2`
- Quick forms: `0`
- Subgrids: `0`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Hierarchy
- Section `Information`
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `ownerid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Social Profiles`
- Type: `6`

- Tabs: `1`
- Sections: `1`
- Controls: `1`
- Quick forms: `0`
- Subgrids: `1`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## general
- Section `RELATED SOCIAL PROFILES`
  - `subgrid_spaccount` [subgrid] (relationship `Socialprofile_customer_accounts`; target `socialprofile`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={7394679c-9224-4e61-b14c-219aaa7dd941}; IsUserView=false; RelationshipName=Socialprofile_customer_accounts; TargetEntityType=socialprofile; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=3; EnableContextualActions=false`)

## Embedded Control Details
- `subgrid_spaccount` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={7394679c-9224-4e61-b14c-219aaa7dd941}; IsUserView=false; RelationshipName=Socialprofile_customer_accounts; TargetEntityType=socialprofile; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=3; EnableContextualActions=false`; path `general / RELATED SOCIAL PROFILES`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account Card form`
- Type: `11`

- Tabs: `1`
- Sections: `4`
- Controls: `6`
- Quick forms: `0`
- Subgrids: `0`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Account Card
- Section `ColorStrip`
- Section `Header`
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `ownerid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
- Section `Details`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_city` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
- Section `Footer`
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `emailaddress1` [field] (class `ada2203e-b4cd-49be-9ddf-234642b43b52`)

## Embedded Control Details
- `emailaddress1` (field; class `ada2203e-b4cd-49be-9ddf-234642b43b52`; path `Account Card / Footer`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account Reference Panel`
- Type: `6`

- Tabs: `1`
- Sections: `2`
- Controls: `11`
- Quick forms: `0`
- Subgrids: `0`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## tab_1
- Section `ACCOUNT DETAILS`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `revenue` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `numberofemployees` [number] (class `c6d124ca-7eda-4a60-aea9-7fb8d318b68f`)
  - `address1_line1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_line2` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_city` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_postalcode` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `ownerid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
- Section `OTHER DETAILS`
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `emailaddress1` [field] (class `ada2203e-b4cd-49be-9ddf-234642b43b52`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)

## Embedded Control Details
- `revenue` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `tab_1 / ACCOUNT DETAILS`)
- `emailaddress1` (field; class `ada2203e-b4cd-49be-9ddf-234642b43b52`; path `tab_1 / OTHER DETAILS`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account for Interactive experience`
- Type: `2`

- Tabs: `2`
- Sections: `8`
- Controls: `27`
- Quick forms: `1`
- Subgrids: `1`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Header
- `revenue` (field)
- `numberofemployees` (number)
- `ownerid` (lookup)

## Summary
- Section `ACCOUNT INFORMATION`
  - `primarycontactid` [quickform] (quick forms `contact:29de27bc-a257-4f29-99cf-bab4a84e688f`; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29de27bc-a257-4f29-99cf-bab4a84e688f; ControlMode=Edit; DisplayAsCustomer360Tile=true`)
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `fax` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `websiteurl` [field] (class `71716b6c-711e-476c-8ab8-5d11542bfb47`)
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `parentaccountid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `address1_composite` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)
- Section `TIMELINE`
  - `notescontrol` [field] (class `06375649-c143-495e-a496-c962e5b4488e`)
- Section `RELATED`
  - `Contacts` [subgrid] (relationship `contact_customer_accounts`; target `contact`; class `02d4264b-47e2-4b4c-aa95-f439f3f4d458`; parameters `ViewId={73BC2D9B-4E0E-424C-8839-ED59D6817E3A}; IsUserView=false; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=6; ReferencePanelSubgridIconUrl=/_imgs/FormEditorRibbon/Contact_16.png`)

## Details
- Section `COMPANY PROFILE`
  - `industrycode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `sic` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `ownershipcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `DESCRIPTION`
  - `description` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)
- Section `CONTACT PREFERENCES`
  - `preferredcontactmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `donotemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `followemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotbulkemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotphone` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotfax` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotpostalmail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
- Section `BILLING`
  - `transactioncurrencyid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `creditlimit` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `creditonhold` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `paymenttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `SHIPPING`
  - `address1_shippingmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `address1_freighttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)

## Embedded Control Details
- `primarycontactid` (quickform; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29de27bc-a257-4f29-99cf-bab4a84e688f; ControlMode=Edit; DisplayAsCustomer360Tile=true`; path `Summary / ACCOUNT INFORMATION`)
- `websiteurl` (field; class `71716b6c-711e-476c-8ab8-5d11542bfb47`; path `Summary / ACCOUNT INFORMATION`)
- `notescontrol` (field; class `06375649-c143-495e-a496-c962e5b4488e`; path `Summary / TIMELINE`)
- `Contacts` (subgrid; class `02d4264b-47e2-4b4c-aa95-f439f3f4d458`; parameters `ViewId={73BC2D9B-4E0E-424C-8839-ED59D6817E3A}; IsUserView=false; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=6; ReferencePanelSubgridIconUrl=/_imgs/FormEditorRibbon/Contact_16.png`; path `Summary / RELATED`)
- `creditlimit` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `Details / BILLING`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `account card`
- Type: `6`

- Tabs: `1`
- Sections: `1`
- Controls: `3`
- Quick forms: `0`
- Subgrids: `0`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## General
- Section `CUSTOMER DETAILS`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `emailaddress1` [field] (class `ada2203e-b4cd-49be-9ddf-234642b43b52`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)

## Embedded Control Details
- `emailaddress1` (field; class `ada2203e-b4cd-49be-9ddf-234642b43b52`; path `General / CUSTOMER DETAILS`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Information`
- Type: `2`

- Tabs: `5`
- Sections: `12`
- Controls: `45`
- Quick forms: `0`
- Subgrids: `2`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Header
- `primarycontactid` (lookup)
- `preferredcontactmethodcode` (optionset)
- `ownerid` (lookup)
- `creditlimit` (field)
- `revenue` (field)

## General
- Section `Account Information`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `telephone2` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `accountnumber` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `fax` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `parentaccountid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `websiteurl` [field] (class `71716b6c-711e-476c-8ab8-5d11542bfb47`)
  - `emailaddress1` [field] (class `ada2203e-b4cd-49be-9ddf-234642b43b52`)
- Section `Address`
  - `address1_addresstypecode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `address1_city` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_stateorprovince` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_line1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_postalcode` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_line2` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_country` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_line3` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
- Section `Shipping Information`
  - `address1_shippingmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `address1_freighttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `Description`
  - `description` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)

## Details
- Section `Professional Information`
  - `industrycode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `revenue` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `ownershipcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `numberofemployees` [number] (class `c6d124ca-7eda-4a60-aea9-7fb8d318b68f`)
  - `tickersymbol` [field] (class `1e1fc551-f7a8-43af-ac34-a8dc35c7b6d4`)
  - `sic` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
- Section `Description`
  - `accountcategorycode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `customertypecode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `Billing Information`
  - `transactioncurrencyid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `paymenttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `creditlimit` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `creditonhold` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)

## Contacts
- Section `Contacts`
  - `accountContactsGrid` [subgrid] (relationship `contact_customer_accounts`; target `contact`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001003}; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=true; EnableJumpBar=false; EnableChartPicker=false; RecordsPerPage=10`)

## Notes & Activities
- Section `Activities`
  - `accountactivitiesgrid` [subgrid] (relationship `Account_ActivityPointers`; target `activitypointer`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001900}; RelationshipName=Account_ActivityPointers; TargetEntityType=activitypointer; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=true; EnableJumpBar=false; EnableChartPicker=false; RecordsPerPage=10`)
- Section `Notes`
  - `notescontrol` [field] (class `06375649-c143-495e-a496-c962e5b4488e`)

## Preferences
- Section `Internal Information`
  - `ownerid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
- Section `Contact Methods`
  - `preferredcontactmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `followemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotbulkemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotfax` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotphone` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotpostalmail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)

## Embedded Control Details
- `websiteurl` (field; class `71716b6c-711e-476c-8ab8-5d11542bfb47`; path `General / Account Information`)
- `emailaddress1` (field; class `ada2203e-b4cd-49be-9ddf-234642b43b52`; path `General / Account Information`)
- `revenue` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `Details / Professional Information`)
- `tickersymbol` (field; class `1e1fc551-f7a8-43af-ac34-a8dc35c7b6d4`; path `Details / Professional Information`)
- `creditlimit` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `Details / Billing Information`)
- `accountContactsGrid` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001003}; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=true; EnableJumpBar=false; EnableChartPicker=false; RecordsPerPage=10`; path `Contacts / Contacts`)
- `accountactivitiesgrid` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001900}; RelationshipName=Account_ActivityPointers; TargetEntityType=activitypointer; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=true; EnableJumpBar=false; EnableChartPicker=false; RecordsPerPage=10`; path `Notes & Activities / Activities`)
- `notescontrol` (field; class `06375649-c143-495e-a496-c962e5b4488e`; path `Notes & Activities / Notes`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account Quick Create`
- Type: `7`

- Tabs: `1`
- Sections: `3`
- Controls: `10`
- Quick forms: `0`
- Subgrids: `0`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Tab
- Section `Details`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `primarycontactid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
- Section `Description`
  - `revenue` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `numberofemployees` [number] (class `c6d124ca-7eda-4a60-aea9-7fb8d318b68f`)
  - `description` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)
- Section `Address`
  - `address1_line1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_line2` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_city` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `address1_postalcode` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)

## Embedded Control Details
- `revenue` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `Tab / Description`)


---


- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-process-policy\readback\readback.json`
- Entity: `account`
- Form: `Account`
- Type: `2`

- Tabs: `2`
- Sections: `12`
- Controls: `32`
- Quick forms: `1`
- Subgrids: `2`
- Web resources: `0`
- Custom control hosts: `2`
- Custom control instances: `8`
- Custom control families: `MscrmControls.CardFeedContainer.CardFeedContainer, MscrmControls.ModelForm.ModelFormControl, id:{270BD3DB-D9AF-4782-9025-509E298DEC0A}`
- Custom control configurations: `2`
- Custom control variants: `8`

## Header
- `revenue` (field)
- `numberofemployees` (number)
- `ownerid` (lookup)

## Summary
- Section `ACCOUNT INFORMATION`
  - `name` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `telephone1` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `fax` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `websiteurl` [field] (class `71716b6c-711e-476c-8ab8-5d11542bfb47`)
  - `parentaccountid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `tickersymbol` [field] (class `1e1fc551-f7a8-43af-ac34-a8dc35c7b6d4`)
- Section `ADDRESS`
  - `address1_composite` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)
- Section `MapSection`
  - `mapcontrol` [field] (class `62b0df79-0464-470f-8af7-4483cfea0c7d`; parameters `AddressField=address1_composite`)
- Section `SOCIAL PANE`
  - `notescontrol` [field] (class `06375649-c143-495e-a496-c962e5b4488e`)
- Section `Assistant`
  - `ActionCards` [field] (target `actioncard`; class `f9a8a302-114e-466a-b582-6771b2ae0d92`; parameters `ViewId={92AFD454-0F2E-4397-A1C8-05E37C6AD699}; IsUserView=false; RelationshipName; TargetEntityType=actioncard; AutoExpand=Fixed; EnableViewPicker=false; EnableJumpBar=false; ChartGridMode=All; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=4`)
- Section `Section`
  - `primarycontactid` [lookup] (class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`)
  - `primarycontactid` [field] (class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`)
  - `primarycontactid` [quickform] (quick forms `contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F`; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`)
  - `Contacts` [subgrid] (relationship `contact_customer_accounts`; target `contact`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={73BC2D9B-4E0E-424C-8839-ED59D6817E3A}; IsUserView=false; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=6`)

## Details
- Section `COMPANY PROFILE`
  - `industrycode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `sic` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `ownershipcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `Description`
  - `description` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)
- Section `CONTACT PREFERENCES`
  - `preferredcontactmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `donotemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `followemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotbulkemail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotphone` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotfax` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `donotpostalmail` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
- Section `BILLING`
  - `transactioncurrencyid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
  - `creditlimit` [field] (class `533b9e00-756b-4312-95a0-dc888637ac78`)
  - `creditonhold` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)
  - `paymenttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `SHIPPING`
  - `address1_shippingmethodcode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `address1_freighttermscode` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
- Section `CHILD ACCOUNTS`
  - `ChildAccounts` [subgrid] (relationship `account_parent_account`; target `account`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001002}; IsUserView=false; RelationshipName=account_parent_account; TargetEntityType=account; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=false; ViewIds={00000000-0000-0000-00AA-000010001002},{00000000-0000-0000-00AA-000010001001}; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId={74A622C0-5193-DE11-97D4-00155DA3B01E}; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=4`)

## Control Descriptions
- Control `{3FF9A528-DD50-4ACA-8F10-2E5ED73513AD}` (host kind `field`; families `MscrmControls.CardFeedContainer.CardFeedContainer`)
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `2`; datasets `SubGrid`; mode `embedded-manifest`)
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `0`; datasets `SubGrid`; mode `embedded-manifest`)
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `1`; datasets `SubGrid`; mode `embedded-manifest`)
- Control `{d3a69167-9d39-4dc5-9c81-6ad8522c7f8e}` (host field `primarycontactid`; host kind `field`; families `MscrmControls.ModelForm.ModelFormControl, id:{270BD3DB-D9AF-4782-9025-509E298DEC0A}`)
  - `{270BD3DB-D9AF-4782-9025-509E298DEC0A}` (form factor ``; field `primarycontactid`; mode `registration-reference`)
  - `{270BD3DB-D9AF-4782-9025-509E298DEC0A}` (form factor ``; field `primarycontactid`; mode `registration-reference`)
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `0`; bind `primarycontactid`; default view `{A2D479C5-53E3-4C69-ADDD-802327E67A0D}`; quick forms `contact:1fed44d1-ae68-4a41-bd2b-f13acac4acfa`; mode `embedded-manifest`)
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `2`; bind `primarycontactid`; default view `{A2D479C5-53E3-4C69-ADDD-802327E67A0D}`; quick forms `contact:1fed44d1-ae68-4a41-bd2b-f13acac4acfa`; mode `embedded-manifest`)
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `1`; bind `primarycontactid`; default view `{A2D479C5-53E3-4C69-ADDD-802327E67A0D}`; quick forms `contact:1fed44d1-ae68-4a41-bd2b-f13acac4acfa`; mode `embedded-manifest`)
- Custom control configuration `3ff9a528-dd50-4aca-8f10-2e5ed73513ad` (version `0`; overall version `0`)
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `2`; control id `00000000-0000-0000-0000-000000000000`; override visible `True`)
    - SubGrid -> actioncard
    - EntityTypeCode=1
    - Location=1
    - msinternal.isvisibleinmocaonly=true
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `0`; control id `00000000-0000-0000-0000-000000000000`; override visible `True`)
    - SubGrid -> actioncard
    - EntityTypeCode=1
    - Location=1
    - msinternal.isvisibleinmocaonly=true
  - `MscrmControls.CardFeedContainer.CardFeedContainer` (form factor `1`; control id `00000000-0000-0000-0000-000000000000`; override visible `True`)
    - SubGrid -> actioncard
    - EntityTypeCode=1
    - Location=1
    - msinternal.isvisibleinmocaonly=true
- Custom control configuration `d3a69167-9d39-4dc5-9c81-6ad8522c7f8e` (version `0`; overall version `0`)
  - `custom-control` (form factor `-1`; control id `270bd3db-d9af-4782-9025-509e298dec0a`; override visible `False`)
    - datafieldname=primarycontactid
  - `custom-control` (form factor `-1`; control id `270bd3db-d9af-4782-9025-509e298dec0a`; override visible `False`)
    - datafieldname=primarycontactid
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `0`; control id `00000000-0000-0000-0000-000000000000`; override visible `False`)
    - value=primarycontactid{A2D479C5-53E3-4C69-ADDD-802327E67A0D}falsefalsefalse
    - QuickForms=<QuickForms><QuickFormIds><QuickFormId entityname="contact">1fed44d1-ae68-4a41-bd2b-f13acac4acfa</QuickFormId></QuickFormIds></QuickForms>
    - SaveMode=0
    - EnableHighDensityPageHeader=false
    - DisplayFormSelector=false
    - AddToRecentItems=false
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `2`; control id `00000000-0000-0000-0000-000000000000`; override visible `False`)
    - value=primarycontactid{A2D479C5-53E3-4C69-ADDD-802327E67A0D}falsefalsefalse
    - QuickForms=<QuickForms><QuickFormIds><QuickFormId entityname="contact">1fed44d1-ae68-4a41-bd2b-f13acac4acfa</QuickFormId></QuickFormIds></QuickForms>
    - SaveMode=0
    - EnableHighDensityPageHeader=false
    - DisplayFormSelector=false
    - AddToRecentItems=false
  - `MscrmControls.ModelForm.ModelFormControl` (form factor `1`; control id `00000000-0000-0000-0000-000000000000`; override visible `False`)
    - value=primarycontactid{A2D479C5-53E3-4C69-ADDD-802327E67A0D}falsefalsefalse
    - QuickForms=<QuickForms><QuickFormIds><QuickFormId entityname="contact">1fed44d1-ae68-4a41-bd2b-f13acac4acfa</QuickFormId></QuickFormIds></QuickForms>
    - SaveMode=0
    - EnableHighDensityPageHeader=false
    - DisplayFormSelector=false
    - AddToRecentItems=false

## Embedded Control Details
- `websiteurl` (field; class `71716b6c-711e-476c-8ab8-5d11542bfb47`; path `Summary / ACCOUNT INFORMATION`)
- `tickersymbol` (field; class `1e1fc551-f7a8-43af-ac34-a8dc35c7b6d4`; path `Summary / ACCOUNT INFORMATION`)
- `mapcontrol` (field; class `62b0df79-0464-470f-8af7-4483cfea0c7d`; parameters `AddressField=address1_composite`; path `Summary / MapSection`)
- `notescontrol` (field; class `06375649-c143-495e-a496-c962e5b4488e`; path `Summary / SOCIAL PANE`)
- `ActionCards` (field; class `f9a8a302-114e-466a-b582-6771b2ae0d92`; parameters `ViewId={92AFD454-0F2E-4397-A1C8-05E37C6AD699}; IsUserView=false; RelationshipName; TargetEntityType=actioncard; AutoExpand=Fixed; EnableViewPicker=false; EnableJumpBar=false; ChartGridMode=All; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=4`; path `Summary / Assistant`)
- `primarycontactid` (lookup; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`; path `Summary / Section`)
- `primarycontactid` (field; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`; path `Summary / Section`)
- `primarycontactid` (quickform; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> contact:29DE27BC-A257-4F29-99CF-BAB4A84E688F; ControlMode=Edit`; path `Summary / Section`)
- `Contacts` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={73BC2D9B-4E0E-424C-8839-ED59D6817E3A}; IsUserView=false; RelationshipName=contact_customer_accounts; TargetEntityType=contact; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=6`; path `Summary / Section`)
- `creditlimit` (field; class `533b9e00-756b-4312-95a0-dc888637ac78`; path `Details / BILLING`)
- `ChildAccounts` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={00000000-0000-0000-00AA-000010001002}; IsUserView=false; RelationshipName=account_parent_account; TargetEntityType=account; AutoExpand=Fixed; EnableQuickFind=false; EnableViewPicker=false; ViewIds={00000000-0000-0000-00AA-000010001002},{00000000-0000-0000-00AA-000010001001}; EnableJumpBar=false; ChartGridMode=Grid; VisualizationId={74A622C0-5193-DE11-97D4-00155DA3B01E}; IsUserChart=false; EnableChartPicker=false; RecordsPerPage=4`; path `Details / CHILD ACCOUNTS`)

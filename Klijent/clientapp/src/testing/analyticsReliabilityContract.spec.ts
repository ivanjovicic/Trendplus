import {
  createFixtureAdapter,
  registerAnalyticsReliabilityContractSuite,
} from "./analyticsReliabilityContract";

registerAnalyticsReliabilityContractSuite(createFixtureAdapter("Pre/Post"));
registerAnalyticsReliabilityContractSuite(createFixtureAdapter("Shoe Type"));
registerAnalyticsReliabilityContractSuite(createFixtureAdapter("Color"));
registerAnalyticsReliabilityContractSuite(createFixtureAdapter("Daily Sales"));
registerAnalyticsReliabilityContractSuite(createFixtureAdapter("Inventory"));

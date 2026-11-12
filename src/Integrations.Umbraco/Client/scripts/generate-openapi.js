import fetch from 'node-fetch';
import chalk from 'chalk';
import https from 'node:https';
import { createClient, defaultPlugins } from '@hey-api/openapi-ts';

// Start notifying user we are generating the TypeScript client
console.log(chalk.green("Generating OpenAPI client..."));

const swaggerUrl = process.argv[2];
if (swaggerUrl === undefined) {
  console.error(chalk.red(`ERROR: Missing URL to OpenAPI spec`));
  console.error(`Please provide the URL to the OpenAPI spec as the first argument found in ${chalk.yellow('package.json')}`);
  console.error(`Example: node generate-openapi.js ${chalk.yellow('https://localhost:44331/umbraco/openapi/relewisedashboard.json')}`);
  process.exit(1);
}

// Allow a development certificate only for the local schema request.
const schemaUrl = new URL(swaggerUrl);
const localHttps = schemaUrl.protocol === 'https:' && ['localhost', '127.0.0.1', '[::1]'].includes(schemaUrl.hostname);

// Start checking to see if we can connect to the OpenAPI spec
console.log("Ensure your Umbraco instance is running");
console.log(`Fetching OpenAPI definition from ${chalk.yellow(swaggerUrl)}`);

fetch(swaggerUrl, { agent: localHttps ? new https.Agent({ rejectUnauthorized: false }) : undefined }).then(async (response) => {
  if (!response.ok) {
    console.error(chalk.red(`ERROR: OpenAPI spec returned with a non OK (200) response: ${response.status} ${response.statusText}`));
    console.error(`The URL to your Umbraco instance may be wrong or the instance is not running`);
    console.error(`Please verify or change the URL in the ${chalk.yellow('package.json')} for the script ${chalk.yellow('generate-openapi')}`);
    process.exitCode = 1;
    return;
  }

  console.log(`OpenAPI spec fetched successfully`);
  console.log(`Calling ${chalk.yellow('hey-api')} to generate TypeScript client`);

  const schema = await response.json();
  // The runtime auth context supplies the host. Do not commit the local generation URL.
  delete schema.servers;
  await createClient({
    input: schema,
    output: 'src/api',
    plugins: [
      ...defaultPlugins,
      '@hey-api/client-fetch',
      {
        name: '@hey-api/typescript',
        enums: 'typescript'
      },
      {
        name: '@hey-api/sdk',
        asClass: true
      }
    ],
  });

})
  .catch(error => {
    process.exitCode = 1;
    console.error(`ERROR: Failed to connect to the OpenAPI spec: ${chalk.red(error.message)}`);
    console.error(`The URL to your Umbraco instance may be wrong or the instance is not running`);
    console.error(`Please verify or change the URL in the ${chalk.yellow('package.json')} for the script ${chalk.yellow('generate-openapi')}`);
  });

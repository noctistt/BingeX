const { transform } = require('sucrase');
globalThis.__nuclearTranspile = source => transform(source, { transforms: ['typescript', 'imports'], disableESTransforms: true, production: true }).code;

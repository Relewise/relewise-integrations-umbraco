// Local wire-contract fixture, based on the installed C# SDK request/response models.
// This checks routing and serialization; it does not model Relewise relevance or NLP.
const http = require('http');
const stateKey = 'relewise-smoke-documents';
// A single-session local fixture. pm.state on this Windows runtime fails with an
// OpenSSL entropy error; process-local state also resets when the simulation stops.
const localState = new Map();
const value = data => data?.Value ?? data?.value;
const property = (object, key) => object?.[key] ?? object?.[key[0].toLowerCase() + key.slice(1)];
const kind = object => property(object, 'TypeName') || String(object?.$type || '').split(',')[0].split('.').pop();
function condition(c, data) {
  const stored = value(data), expected = value(property(c, 'Value'));
  let result;
  switch (kind(c)) {
    case 'EqualsCondition': result = JSON.stringify(stored) === JSON.stringify(expected); break;
    case 'HasValueCondition': result = stored != null && (!Array.isArray(stored) || stored.length > 0); break;
    case 'ContainsCondition': result = Array.isArray(stored) ? stored.includes(expected) : String(stored).includes(expected); break;
    default: throw new Error('Fixture does not implement condition ' + kind(c));
  }
  return property(c, 'Negated') ? !result : result;
}
function matches(filter, document) {
  let result;
  switch (kind(filter)) {
    case 'AndFilter': result = property(filter, 'Filters').every(f => matches(f, document)); break;
    case 'OrFilter': result = property(filter, 'Filters').some(f => matches(f, document)); break;
    case 'ContentIdFilter': result = property(filter, 'ContentIds').includes(property(document, 'Id')); break;
    case 'ContentDataFilter': {
      const data = property(document, 'Data')?.[property(filter, 'Key')];
      if (data == null) { result = !property(filter, 'FilterOutIfKeyIsNotFound'); break; }
      const conditions = property(property(filter, 'Conditions'), 'Items');
      result = property(filter, 'MustMatchAllConditions') ? conditions.every(c => condition(c, data)) : conditions.some(c => condition(c, data));
      break;
    }
    default: throw new Error('Fixture does not implement filter ' + kind(filter));
  }
  return property(filter, 'Negated') ? !result : result;
}
function filtered(request, documents) {
  return documents.filter(d => (property(property(request, 'Filters'), 'Items') || []).every(f => matches(f, d)));
}
http.createServer(async (req, res) => {
  res.setHeader('Content-Type', 'application/json');
  const reading = new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', chunk => chunks.push(chunk));
    req.on('end', () => {
      try {
        let buffer = Buffer.concat(chunks);
        if (req.headers['content-encoding'] === 'gzip') buffer = require('zlib').gunzipSync(buffer);
        resolve(buffer.length ? JSON.parse(buffer.toString()) : (req.body || {}));
      } catch (error) { reject(error); }
    });
  });
  try {
    const body = await reading;
    let documents = localState.get(stateKey) || [];
    if (req.method === 'GET' && req.url === '/smoke/last') { res.end(JSON.stringify(localState.get('relewise-smoke-last'))); return; }
    if (req.method === 'GET' && req.url === '/smoke/state') { res.end(JSON.stringify(documents)); return; }
    if (req.method !== 'POST') { res.statusCode = 501; res.end('{}'); return; }
    localState.set('relewise-smoke-last', { path: req.url, body });
    if (/\/v1\/(BatchedTrackingRequest|TrackContentUpdateRequest|TrackContentAdministrativeActionRequest)$/.test(req.url)) {
      for (const item of property(body, 'Items') || [property(body, 'AdministrativeAction') || property(body, 'ContentUpdate')]) {
        if (kind(item) === 'ContentUpdate') {
          const content = property(item, 'Content');
          documents = documents.filter(d => property(d, 'Id') !== property(content, 'Id'));
          documents.push(content);
        } else if (kind(item) === 'ContentAdministrativeAction') {
          const selected = new Set(filtered(item, documents).map(d => property(d, 'Id')));
          if (property(item, 'Kind') === 'Delete' || property(item, 'Kind') === 2) documents = documents.filter(d => !selected.has(property(d, 'Id')));
        } else throw new Error('Fixture does not implement trackable ' + kind(item));
      }
      localState.set(stateKey, documents);
      res.end('{}'); return;
    }
    if (/\/v1\/ContentSearchRequest$/.test(req.url)) {
      let matches = filtered(body, documents);
      const term = property(body, 'Term');
      if (term) matches = matches.filter(d => JSON.stringify(property(d, 'DisplayName')).toLowerCase().includes(term.toLowerCase()));
      const skip = property(body, 'Skip') || 0, take = property(body, 'Take') || 0;
      res.end(JSON.stringify({ Hits: matches.length, Results: matches.slice(skip, skip + take).map((d, rank) => ({ ContentId: property(d, 'Id'), Rank: rank, Data: property(d, 'Data') })), Facets: { Items: [] }, Statistics: { ServerTimeInMs: 1 } }));
      return;
    }
    res.statusCode = 501; res.end(JSON.stringify({ error: 'Unmodeled SDK path', path: req.url }));
  } catch (error) { res.statusCode = 500; res.end(JSON.stringify({ error: error.message })); }
}).listen(process.env.PORT);

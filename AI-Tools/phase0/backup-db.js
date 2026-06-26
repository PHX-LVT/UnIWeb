const fs = require("fs");
const path = require("path");

const databaseName = "FullProjectDbVersion2";
const outDir = process.env.PHASE0_BACKUP_DIR;

if (!outDir) {
  throw new Error("Set PHASE0_BACKUP_DIR to the backup output directory.");
}

fs.mkdirSync(outDir, { recursive: true });

const database = db.getSiblingDB(databaseName);
const collections = database.getCollectionNames().sort();

const manifest = {
  database: databaseName,
  createdAt: new Date().toISOString(),
  collections: []
};

for (const name of collections) {
  const docs = database.getCollection(name).find({}).toArray();
  const fileName = `${name}.json`;
  fs.writeFileSync(path.join(outDir, fileName), EJSON.stringify(docs, null, 2), "utf8");
  manifest.collections.push({ name, count: docs.length, fileName });
}

fs.writeFileSync(path.join(outDir, "manifest.json"), JSON.stringify(manifest, null, 2), "utf8");
printjson(manifest);

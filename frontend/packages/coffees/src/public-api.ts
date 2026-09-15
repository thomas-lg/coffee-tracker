/*
 * Public API surface of @coffee-tracker/coffees
 */

export * from './lib/coffees.routes';
// The landing route lives in app.routes.ts, so Home is the one page that has to be
// reachable from outside. The other three are only ever loaded by COFFEES_ROUTES.
export * from './lib/pages/home/home';
export * from './lib/components/bean-scene/bean-scene';
export * from './lib/components/coffee-card/coffee-card';
export * from './lib/components/coffee-card-skeleton/coffee-card-skeleton';
export * from './lib/components/coffee-shelf-states/coffee-shelf-states';
export * from './lib/services/coffees.store';

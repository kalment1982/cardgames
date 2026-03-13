(function () {
  if (window.tractorTest) {
    return;
  }

  const events = [];

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  window.tractorTest = {
    clearEvents() {
      events.length = 0;
    },
    pushEvent(event) {
      events.push({
        timestamp: new Date().toISOString(),
        ...event,
      });
    },
    getEvents() {
      return clone(events);
    },
    getLastEvent() {
      if (events.length === 0) {
        return null;
      }
      return clone(events[events.length - 1]);
    },
  };
})();

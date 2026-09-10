package com.nonaconfig.client;

import org.junit.Test;
import java.util.Collections;
import java.util.concurrent.TimeUnit;
import static org.junit.Assert.*;

public class JavaApiTest {
    @Test public void javaClientCanFetchActivateRestoreAndReset() throws Exception {
        NonaOptions options = NonaOptions.builder("https://nona.test", "Production")
            .apiKey("frontend").minimumFetchIntervalMillis(0)
            .connectTimeoutMillis(1000).readTimeoutMillis(1000).build();
        InMemorySnapshotStore store = new InMemorySnapshotStore();
        NonaConfig config = NonaConfig.create(options, store, (url, headers) ->
            new NonaHttpResponse(200, "{\"flag\":{\"value\":\"true\",\"contentType\":\"boolean\"}}", "a"));
        config.setDefaults(Collections.singletonMap("flag", false));
        assertFalse(config.initializeAsync().get(5, TimeUnit.SECONDS));
        assertEquals(FetchStatus.SUCCESS, config.fetchAsync().get(5, TimeUnit.SECONDS));
        assertFalse(config.getBoolean("flag"));
        assertTrue(config.activate());
        assertTrue(config.getBoolean("flag"));
        assertFalse(config.fetchAndActivateAsync().get(5, TimeUnit.SECONDS));
        config.resetAsync().get(5, TimeUnit.SECONDS);
        assertNull(store.read());
    }
}

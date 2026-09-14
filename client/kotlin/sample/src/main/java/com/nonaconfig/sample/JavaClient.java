package com.nonaconfig.sample;

import android.content.Context;
import com.nonaconfig.client.NonaConfig;
import com.nonaconfig.client.NonaOptions;

/** Compiled into the sample to exercise the public Java entry points. */
public final class JavaClient {
    private JavaClient() {}

    public static NonaConfig create(Context context, String baseUrl, String apiKey) {
        NonaOptions options = NonaOptions.builder(baseUrl, "Production")
            .apiKey(apiKey).minimumFetchIntervalMillis(0)
            .connectTimeoutMillis(1500).readTimeoutMillis(1500).build();
        return NonaConfig.create(context, options);
    }
}

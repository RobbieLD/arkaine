-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.**
-keep,includedescriptorclasses class dev.robdavis.arkaine.**$$serializer { *; }
-keepclassmembers class dev.robdavis.arkaine.** {
    *** Companion;
}
-keepclasseswithmembers class dev.robdavis.arkaine.** {
    kotlinx.serialization.KSerializer serializer(...);
}
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**
